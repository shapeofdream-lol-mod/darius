Set-StrictMode -Version Latest

function Get-DariusNativeProfiles {
    param([Parameter(Mandatory = $true)][string]$ProfilePath)

    $text = Get-Content $ProfilePath -Raw
    $options = [System.Text.RegularExpressions.RegexOptions]::Singleline
    $blocks = [regex]::Matches($text, 'new\s+DariusNativeSkinProfile\s*\{(?<body>.*?)\};', $options)
    $profiles = @()
    foreach ($block in $blocks) {
        $body = $block.Groups['body'].Value
        $variant = [regex]::Match($body, 'Variant\s*=\s*"(?<v>[^"]+)"')
        $glb = [regex]::Match($body, 'GlbFile\s*=\s*"(?<v>[^"]+)"')
        $fbx = [regex]::Match($body, 'FbxFile\s*=\s*"(?<v>[^"]+)"')
        if (-not $variant.Success -or -not $glb.Success -or -not $fbx.Success) {
            throw "Native skin profile block is missing Variant/GlbFile/FbxFile: $body"
        }
        $profiles += [pscustomobject]@{
            Variant = $variant.Groups['v'].Value
            Input = $glb.Groups['v'].Value
            Output = $fbx.Groups['v'].Value
        }
    }

    if ($profiles.Count -eq 0) { throw "No native skin profiles found: $ProfilePath" }
    foreach ($property in 'Variant', 'Input', 'Output') {
        if (($profiles.$property | Select-Object -Unique).Count -ne $profiles.Count) {
            throw "Duplicate $property entries in shared native profile: $ProfilePath"
        }
    }
    return $profiles
}

function Get-DariusNativeFingerprint {
    param(
        [Parameter(Mandatory = $true)][string]$RepoRoot,
        [Parameter(Mandatory = $true)][string]$ProfilePath,
        [Parameter(Mandatory = $true)][object[]]$Profiles
    )

    $repo = (Resolve-Path $RepoRoot).Path
    $trimChars = [char[]]@([System.IO.Path]::DirectorySeparatorChar, [System.IO.Path]::AltDirectorySeparatorChar)
    $repoPrefix = $repo.TrimEnd($trimChars) + [System.IO.Path]::DirectorySeparatorChar
    $inputs = New-Object System.Collections.Generic.List[string]
    $inputs.Add((Resolve-Path $ProfilePath).Path)
    foreach ($relative in @(
        'tools\DariusUnityAssets\convert_glb_to_fbx.py',
        'tools\DariusUnityAssets\Build-NativeModels.ps1',
        'tools\DariusUnityAssets\Native-Model-Contract.ps1',
        'src\DariusPrototype\Native\DariusNativeAssetContract.cs'
    )) {
        $inputs.Add((Resolve-Path (Join-Path $repo $relative)).Path)
    }

    Get-ChildItem (Join-Path $repo 'tools\DariusUnityAssets\Editor') -Filter '*.cs' -File |
        Sort-Object Name |
        ForEach-Object { $inputs.Add($_.FullName) }
    foreach ($profile in $Profiles) {
        $inputs.Add((Resolve-Path (Join-Path $repo (Join-Path 'assets\models' $profile.Input))).Path)
    }

    $records = foreach ($path in ($inputs | Sort-Object -Unique)) {
        if (-not (Test-Path $path -PathType Leaf)) { throw "Native fingerprint input missing: $path" }
        $fullPath = [System.IO.Path]::GetFullPath($path)
        if (-not $fullPath.StartsWith($repoPrefix, [System.StringComparison]::OrdinalIgnoreCase)) {
            throw "Native fingerprint input is outside repository root: $fullPath"
        }
        $relative = $fullPath.Substring($repoPrefix.Length).Replace([System.IO.Path]::DirectorySeparatorChar, [char]'/')
        $hash = (Get-FileHash $path -Algorithm SHA256).Hash.ToLowerInvariant()
        "$relative`t$hash"
    }
    $payload = ($records -join "`n") + "`n"
    $bytes = [System.Text.Encoding]::UTF8.GetBytes($payload)
    $sha = [System.Security.Cryptography.SHA256]::Create()
    try {
        return ([System.BitConverter]::ToString($sha.ComputeHash($bytes))).Replace('-', '').ToLowerInvariant()
    } finally {
        $sha.Dispose()
    }
}

function Test-DariusNativeBundleFingerprint {
    param(
        [Parameter(Mandatory = $true)][string]$RepoRoot,
        [Parameter(Mandatory = $true)][string]$ProfilePath
    )

    $repo = (Resolve-Path $RepoRoot).Path
    $bundle = Join-Path $repo 'assets\models\darius_models.bundle'
    $stamp = $bundle + '.fingerprint'
    if (-not (Test-Path $bundle -PathType Leaf)) { throw "Native model bundle missing: $bundle" }
    if (-not (Test-Path $stamp -PathType Leaf)) { throw "Native model fingerprint missing: $stamp" }

    $profiles = @(Get-DariusNativeProfiles -ProfilePath $ProfilePath)
    $expected = Get-DariusNativeFingerprint -RepoRoot $repo -ProfilePath $ProfilePath -Profiles $profiles
    $actual = (Get-Content $stamp -Raw).Trim().ToLowerInvariant()
    if ($actual -ne $expected) {
        throw "Native model bundle is stale. Rebuild with tools/DariusUnityAssets/Build-NativeModels.ps1. expected=$expected actual=$actual"
    }
    return $expected
}
