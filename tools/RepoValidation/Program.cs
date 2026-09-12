using System.Diagnostics;
using System.Text;
using System.Text.Json;
using System.Xml.Linq;

var root = GetRoot(args);
var failures = new List<string>();
var skips = new List<string>();

Run("metadata/workshop", CheckMetadata);
Run("MSBuild/package contract", CheckProject);
Run("tracked files", CheckTrackedFiles);
Run("voice source contract", CheckVoiceSource);
Run("audio assets", CheckAudioAssets);
Run("GLB assets", CheckModels);
Run("package snapshot", CheckPackage);

Console.WriteLine();
Console.WriteLine($"Repository validation: {7 - failures.Count - skips.Count} passed, {skips.Count} skipped, {failures.Count} failed");
foreach (var skip in skips) Console.WriteLine("SKIP " + skip);
foreach (var failure in failures) Console.Error.WriteLine("FAIL " + failure);
return failures.Count == 0 ? 0 : 1;

void Run(string name, Action action)
{
    try { action(); Console.WriteLine("PASS " + name); }
    catch (SkipException e) { skips.Add(name + ": " + e.Message); }
    catch (Exception e) { failures.Add(name + ": " + e.Message); }
}

void CheckMetadata()
{
    using var doc = JsonDocument.Parse(File.ReadAllText(P("about", "metadata.json"), Encoding.UTF8));
    var json = doc.RootElement;
    Need(json.ValueKind == JsonValueKind.Object, "metadata.json must be an object");
    foreach (var key in new[] { "id", "name", "author", "modVer" }) Need(!string.IsNullOrWhiteSpace(Str(json, key)), $"metadata.{key} is required");
    var version = Str(json, "modVer");
    Need(Arr(json, "assemblies").EnumerateArray().Any(x => x.GetString() == "DariusPrototype.dll"), "metadata assemblies must contain DariusPrototype.dll");
    foreach (var key in new[] { "dependencies", "loadBefore", "loadAfter" }) _ = Arr(json, key);
    Need(File.ReadAllText(P("CHANGELOG.md"), Encoding.UTF8).Contains($"## [{version}]", StringComparison.Ordinal), "CHANGELOG has no section for metadata modVer");

    var workshopId = File.ReadAllText(P("about", "publishedfileid.txt"), Encoding.UTF8).Trim();
    Need(workshopId.Length > 0 && workshopId.All(char.IsDigit) && ulong.TryParse(workshopId, out _), "publishedfileid.txt must be an unsigned integer");
    var descriptionBytes = Encoding.UTF8.GetByteCount(File.ReadAllText(P("about", "description.txt"), Encoding.UTF8));
    Need(descriptionBytes <= 8000, $"description.txt is {descriptionBytes} UTF-8 bytes; max is 8000");
    if (descriptionBytes > 7500) Console.WriteLine($"WARN description.txt is {descriptionBytes} bytes (warning threshold 7500)");
}

void CheckProject()
{
    var projectPath = P("src", "DariusPrototype", "DariusPrototype.csproj");
    var xml = XDocument.Load(projectPath);
    var x = xml.Root ?? throw new InvalidDataException("csproj has no root");
    string Value(string name) => x.Descendants().First(e => e.Name.LocalName == name).Value.Trim();
    Need(Value("TargetFramework") == "netstandard2.1", "runtime TargetFramework changed");
    Need(Value("AssemblyName") == "DariusPrototype", "runtime AssemblyName changed");
    Need(!x.Descendants().Any(e => e.Name.LocalName == "PackageReference"), "runtime project must stay NuGet-free");

    var targets = x.Descendants().Where(e => e.Name.LocalName == "Target" && e.Attribute("Name") != null)
        .ToDictionary(e => e.Attribute("Name")!.Value, StringComparer.Ordinal);
    XElement T(string name) => targets.TryGetValue(name, out var t) ? t : throw new InvalidDataException("missing target " + name);
    var config = T("CheckPackageConfiguration");
    var voice = T("VerifyVoiceAssets");
    var packageAssets = T("VerifyPackageAssets");
    var package = T("PackageMod");
    _ = T("DeployMod");
    Need(config.Descendants().Any(e => e.Name.LocalName == "Error" && ((string?)e.Attribute("Condition"))?.Contains("Release", StringComparison.Ordinal) == true), "Release preflight is missing");
    Need(((string?)packageAssets.Attribute("DependsOnTargets") ?? "").Split(';').Contains("VerifyVoiceAssets"), "VerifyPackageAssets must depend on VerifyVoiceAssets");
    var deps = ((string?)package.Attribute("DependsOnTargets") ?? "").Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
    Need(Index(deps, "CheckPackageConfiguration") < Index(deps, "VerifyPackageAssets") && Index(deps, "VerifyPackageAssets") < Index(deps, "Build"), "PackageMod dependency order changed");
    Need(voice.Descendants().Any(e => e.Name.LocalName == "_VoiceOgg" && (((string?)e.Attribute("Include")) ?? "").Contains("vo_*.ogg", StringComparison.OrdinalIgnoreCase)), "VerifyVoiceAssets must require vo_*.ogg");
    Need(voice.Descendants().Any(e => e.Name.LocalName == "_LegacyVoiceWav") && voice.Descendants().Any(e => e.Name.LocalName == "Error" && (((string?)e.Attribute("Condition")) ?? "").Contains("@(_LegacyVoiceWav)", StringComparison.Ordinal)), "VerifyVoiceAssets must reject vo_*.wav");
    var audioItem = x.Descendants().FirstOrDefault(e => e.Name.LocalName == "_PackageFile" && Norm((string?)e.Attribute("Include") ?? "").Contains("assets/audio/**", StringComparison.OrdinalIgnoreCase));
    Need(audioItem != null && Norm((string?)audioItem.Attribute("Exclude") ?? "").Contains("vo_*.wav", StringComparison.OrdinalIgnoreCase), "package must exclude vo_*.wav");
    Need(!File.ReadAllText(projectPath).Contains("UseHardlinksIfPossible=\"true\"", StringComparison.OrdinalIgnoreCase), "package must not use hardlinks");
}

void CheckTrackedFiles()
{
    if (!Directory.Exists(P(".git")) && !File.Exists(P(".git"))) throw new SkipException(".git metadata unavailable");
    var files = GitFiles();
    var binary = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { ".dll", ".pdb", ".glb", ".ogg", ".wav", ".wem", ".tex", ".skn", ".png", ".jpg", ".jpeg", ".bmp", ".ico", ".zip", ".7z", ".rar", ".assetbundle" };
    var badBinary = files.Where(f => binary.Contains(Path.GetExtension(f))).ToArray();
    Need(badBinary.Length == 0, "tracked binary assets/build products: " + string.Join(", ", badBinary));
    var shell = files.Where(f => f.EndsWith(".ps1", StringComparison.OrdinalIgnoreCase) || f.EndsWith(".bat", StringComparison.OrdinalIgnoreCase)).ToArray();
    Need(shell.Length == 0, "shell build/validation scripts are not allowed: " + string.Join(", ", shell));
}

void CheckVoiceSource()
{
    var media = File.ReadAllText(P("src", "DariusPrototype", "DariusMedia.cs"), Encoding.UTF8);
    var entry = File.ReadAllText(P("src", "DariusPrototype", "DariusPrototype.cs"), Encoding.UTF8);
    Need(!media.Contains("VoiceLoadsInFlight", StringComparison.Ordinal), "VoiceLoadsInFlight was reintroduced");
    Need(!media.Contains("PreloadVoiceOgg", StringComparison.Ordinal) && !entry.Contains("PreloadVoiceOgg", StringComparison.Ordinal), "PreloadVoiceOgg was reintroduced");
    Need(media.Contains("key + \".ogg\"", StringComparison.Ordinal) && media.Contains("TryPlayVoice2D", StringComparison.Ordinal), "voice runtime is no longer OGG-only/on-demand");
}

void CheckAudioAssets()
{
    var dir = P("assets", "audio");
    if (!Directory.Exists(dir)) throw new SkipException("assets/audio not present");
    Need(Directory.GetFiles(dir, "vo_*.wav", SearchOption.AllDirectories).Length == 0, "legacy vo_*.wav exists");
    Need(Directory.GetFiles(dir, "vo_*.ogg", SearchOption.AllDirectories).Length > 0, "no vo_*.ogg assets found");
    foreach (var file in Directory.GetFiles(dir, "*.ogg", SearchOption.AllDirectories)) Need(Header(file, 4) == "OggS", Path.GetFileName(file) + " has invalid OGG header");
    foreach (var file in Directory.GetFiles(dir, "*.wav", SearchOption.AllDirectories))
    {
        using var fs = File.OpenRead(file); using var r = new BinaryReader(fs, Encoding.ASCII);
        Need(fs.Length >= 44 && Four(r) == "RIFF", Path.GetFileName(file) + " has invalid RIFF header"); _ = r.ReadUInt32(); Need(Four(r) == "WAVE", Path.GetFileName(file) + " has invalid WAVE header");
    }
}

void CheckModels()
{
    var dir = P("assets", "models");
    if (!Directory.Exists(dir)) throw new SkipException("assets/models not present");
    var files = Directory.GetFiles(dir, "*.glb"); Need(files.Length > 0, "no GLB files found");
    foreach (var file in files)
    {
        using var fs = File.OpenRead(file); using var r = new BinaryReader(fs, Encoding.UTF8);
        Need(fs.Length >= 20 && r.ReadUInt32() == 0x46546C67 && r.ReadUInt32() == 2 && r.ReadUInt32() == fs.Length, Path.GetFileName(file) + " has invalid GLB header");
        JsonDocument? json = null;
        while (fs.Position + 8 <= fs.Length)
        {
            var len = r.ReadUInt32(); var type = r.ReadUInt32(); Need(len <= int.MaxValue && fs.Position + len <= fs.Length, Path.GetFileName(file) + " has truncated GLB chunk");
            var bytes = r.ReadBytes((int)len); if (type == 0x4E4F534A && json == null) json = JsonDocument.Parse(Encoding.UTF8.GetString(bytes).TrimEnd('\0', ' ', '\t', '\r', '\n'));
        }
        using var parsed = json ?? throw new InvalidDataException(Path.GetFileName(file) + " has no GLB JSON chunk");
        var j = parsed.RootElement; Need(Arr(j, "meshes").GetArrayLength() > 0 && Arr(j, "skins").GetArrayLength() > 0 && Arr(j, "animations").GetArrayLength() > 0, Path.GetFileName(file) + " must contain meshes, skins and animations");
    }
}

void CheckPackage()
{
    var dir = P("build"); if (!Directory.Exists(dir)) throw new SkipException("build/ not present");
    Need(File.Exists(Path.Combine(dir, "DariusPrototype.dll")), "package DLL missing");
    Need(File.Exists(Path.Combine(dir, "about", "metadata.json")), "package metadata missing");
    Need(File.Exists(Path.Combine(dir, "assets", "raw_lol_audio", "PASS2_MEDIA_MANIEST.json")), "package Pass2 manifest missing");
    var audio = Path.Combine(dir, "assets", "audio"); Need(Directory.Exists(audio) && Directory.GetFiles(audio, "vo_*.ogg", SearchOption.AllDirectories).Length > 0, "package voice OGG missing");
    Need(Directory.GetFiles(audio, "vo_*.wav", SearchOption.AllDirectories).Length == 0, "package contains voice WAV");
    var raw = Path.Combine(dir, "assets", "raw_lol_audio"); Need(Directory.GetFiles(raw, "*", SearchOption.AllDirectories).All(f => Path.GetFileName(f) == "PASS2_MEDIA_MANIFEST.json"), "package contains raw Wwise files");
    Need(!Directory.Exists(Path.Combine(dir, "assets", "raw_lol_vfx_pass2")), "package contains raw LoL VFX extraction tree");
}

string P(params string[] parts) => Path.Combine(new[] { root }.Concat(parts).ToArray());
static string Norm(string value) => value.Replace('\\', '/');
static int Index(string[] values, string value) { var i = Array.IndexOf(values, value); Need(i >= 0, "missing PackageMod dependency " + value); return i; }
static void Need(bool ok, string message) { if (!ok) throw new InvalidDataException(message); }
static string Str(JsonElement e, string name) { Need(e.TryGetProperty(name, out var v) && v.ValueKind == JsonValueKind.String, "missing string " + name); return v.GetString() ?? ""; }
static JsonElement Arr(JsonElement e, string name) { Need(e.TryGetProperty(name, out var v) && v.ValueKind == JsonValueKind.Array, "missing array " + name); return v; }
static string Header(string file, int count) { using var s = File.OpenRead(file); Need(s.Length >= count, Path.GetFileName(file) + " is too small"); var b = new byte[count]; Need(s.Read(b, 0, count) == count, "short read"); return Encoding.ASCII.GetString(b); }
static string Four(BinaryReader r) { var b = r.ReadBytes(4); Need(b.Length == 4, "unexpected EOF"); return Encoding.ASCII.GetString(b); }

IReadOnlyList<string> GitFiles()
{
    var psi = new ProcessStartInfo("git") { RedirectStandardOutput = true, RedirectStandardError = true, UseShellExecute = false };
    foreach (var a in new[] { "-C", root, "ls-files", "-z" }) psi.ArgumentList.Add(a);
    using var p = Process.Start(psi) ?? throw new InvalidOperationException("cannot start git");
    var stdout = p.StandardOutput.ReadToEnd(); var stderr = p.StandardError.ReadToEnd(); p.WaitForExit(); Need(p.ExitCode == 0, "git ls-files failed: " + stderr.Trim());
    return stdout.Split('\0', StringSplitOptions.RemoveEmptyEntries);
}

static string GetRoot(string[] args)
{
    var value = Directory.GetCurrentDirectory();
    for (var i = 0; i < args.Length; i++) { if (args[i] == "--repo" && i + 1 < args.Length) value = args[++i]; else throw new ArgumentException("usage: --repo <path>"); }
    var full = Path.GetFullPath(value); Need(File.Exists(Path.Combine(full, "about", "metadata.json")), "not a Darius repository: " + full); return full;
}

sealed class SkipException(string message) : Exception(message);
