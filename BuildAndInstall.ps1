$ErrorActionPreference = 'Stop'

[Console]::OutputEncoding = [System.Text.Encoding]::UTF8
$OutputEncoding = [System.Text.Encoding]::UTF8
$env:DOTNET_CLI_UI_LANGUAGE = 'en-US'

$ProjectDir = Split-Path -Parent $MyInvocation.MyCommand.Path
$LogFile = Join-Path $ProjectDir 'BUILD_LOG.txt'
$IconDir = Join-Path $ProjectDir 'assets\icons'
$AudioDir = Join-Path $ProjectDir 'assets\audio'

function Log([string]$Text) {
    Add-Content -Path $LogFile -Value $Text -Encoding UTF8
}

function Stage([string]$Text) {
    Write-Host ''
    Write-Host ('=== ' + $Text + ' ===') -ForegroundColor Cyan
    Log ('=== ' + $Text + ' ===')
}


function ConvertFrom-JsonCaseSensitive([string]$JsonText) {
    # Windows PowerShell ConvertFrom-Json stores object properties case-insensitively.
    # Real Riot GLBs can legitimately contain keys/clip metadata that differ only by case
    # (for example Spell4_To_Idle and Spell4_to_Idle). Use JavaScriptSerializer so those
    # entries remain distinct instead of being rejected as duplicated keys.
    try { Add-Type -AssemblyName System.Web.Extensions -ErrorAction Stop } catch { }
    $Serializer = New-Object System.Web.Script.Serialization.JavaScriptSerializer
    $Serializer.MaxJsonLength = [int]::MaxValue
    $Serializer.RecursionLimit = 4096
    return $Serializer.DeserializeObject($JsonText)
}

function Assert-GlbIntegrity([string]$Path, [string]$Label, [int]$ExpectedPrimitives = 0, [int]$ExpectedJoints = 0, [int]$ExpectedAnimations = 0, [string[]]$RequiredClips = @()) {
    if (-not (Test-Path $Path -PathType Leaf)) { throw ($Label + ' GLB missing: ' + $Path) }
    $Stream = $null
    $Reader = $null
    try {
        $Stream = [System.IO.File]::Open($Path, [System.IO.FileMode]::Open, [System.IO.FileAccess]::Read, [System.IO.FileShare]::Read)
        $Reader = New-Object System.IO.BinaryReader($Stream)
        if ($Stream.Length -lt 20) { throw ($Label + ' GLB too small: ' + $Path) }
        $Magic = $Reader.ReadUInt32()
        $Version = $Reader.ReadUInt32()
        $DeclaredLength = $Reader.ReadUInt32()
        if ($Magic -ne 0x46546C67) { throw ($Label + ' is not a binary glTF/GLB file: ' + $Path) }
        if ($Version -ne 2) { throw ($Label + ' GLB version must be 2, got ' + $Version) }
        if ([int64]$DeclaredLength -ne $Stream.Length) { throw ($Label + ' GLB declared length mismatch. header=' + $DeclaredLength + ' actual=' + $Stream.Length) }

        $Json = $null
        while ($Stream.Position -lt $Stream.Length) {
            if (($Stream.Length - $Stream.Position) -lt 8) { throw ($Label + ' GLB has truncated chunk header.') }
            $ChunkLength = $Reader.ReadUInt32()
            $ChunkType = $Reader.ReadUInt32()
            if (($Stream.Position + $ChunkLength) -gt $Stream.Length) { throw ($Label + ' GLB chunk exceeds file length.') }
            $Chunk = $Reader.ReadBytes([int]$ChunkLength)
            if ($ChunkType -eq 0x4E4F534A -and $null -eq $Json) {
                $JsonText = [System.Text.Encoding]::UTF8.GetString($Chunk).Trim([char]0, [char]32, [char]9, [char]13, [char]10)
                $Json = ConvertFrom-JsonCaseSensitive $JsonText
            }
        }
        if ($null -eq $Json) { throw ($Label + ' GLB has no JSON chunk.') }
        $Meshes = @($Json['meshes'])
        $Skins = @($Json['skins'])
        $Animations = @($Json['animations'])
        $Accessors = @($Json['accessors'])
        if ($Meshes.Count -lt 1) { throw ($Label + ' GLB contains no mesh.') }
        if ($Skins.Count -lt 1) { throw ($Label + ' GLB contains no skin/skeleton.') }
        if ($Animations.Count -lt 1) { throw ($Label + ' GLB contains no animations.') }
        $Skin0 = $Skins[0]
        $Joints = @($Skin0['joints'])
        $JointCount = $Joints.Count
        if ($JointCount -lt 1) { throw ($Label + ' GLB skin has no joints.') }
        if ($Skin0.ContainsKey('inverseBindMatrices')) {
            $AccessorIndex = [int]$Skin0['inverseBindMatrices']
            if ($AccessorIndex -lt 0 -or $AccessorIndex -ge $Accessors.Count) { throw ($Label + ' inverseBindMatrices accessor is invalid.') }
            $IbmCount = [int]$Accessors[$AccessorIndex]['count']
            if ($IbmCount -lt $JointCount) { throw ($Label + ' inverseBindMatrices count ' + $IbmCount + ' is smaller than joint count ' + $JointCount) }
        }
        $PrimitiveCount = 0
        foreach ($Mesh in $Meshes) {
            foreach ($Primitive in @($Mesh['primitives'])) {
                $PrimitiveCount++
                $Attrs = $Primitive['attributes']
                if ($null -eq $Attrs -or -not $Attrs.ContainsKey('POSITION')) { throw ($Label + ' primitive missing POSITION attribute.') }
                if (-not $Attrs.ContainsKey('JOINTS_0')) { throw ($Label + ' primitive missing JOINTS_0 attribute.') }
                if (-not $Attrs.ContainsKey('WEIGHTS_0')) { throw ($Label + ' primitive missing WEIGHTS_0 attribute.') }
                if (-not $Primitive.ContainsKey('indices')) { throw ($Label + ' primitive missing index buffer.') }
            }
        }
        if ($PrimitiveCount -lt 1) { throw ($Label + ' GLB contains no renderable primitives.') }
        if ($ExpectedPrimitives -gt 0 -and $PrimitiveCount -ne $ExpectedPrimitives) { throw ($Label + ' primitive count mismatch. expected=' + $ExpectedPrimitives + ' actual=' + $PrimitiveCount) }
        if ($ExpectedJoints -gt 0 -and $JointCount -ne $ExpectedJoints) { throw ($Label + ' joint count mismatch. expected=' + $ExpectedJoints + ' actual=' + $JointCount) }
        $AnimationCount = $Animations.Count
        if ($ExpectedAnimations -gt 0 -and $AnimationCount -ne $ExpectedAnimations) { throw ($Label + ' animation count mismatch. expected=' + $ExpectedAnimations + ' actual=' + $AnimationCount) }
        $AnimationNames = @($Animations | ForEach-Object { [string]$_['name'] })
        foreach ($RequiredClip in @($RequiredClips)) {
            if (-not [string]::IsNullOrWhiteSpace($RequiredClip) -and -not ($AnimationNames -ccontains $RequiredClip)) { throw ($Label + ' missing required animation clip: ' + $RequiredClip) }
        }
        Log ('MODEL GLB VALID: ' + $Label + ' file=' + $Path + ' bytes=' + $Stream.Length + ' joints=' + $JointCount + ' animations=' + $AnimationCount + ' primitives=' + $PrimitiveCount)
    }
    finally {
        if ($null -ne $Reader) { $Reader.Dispose() }
        elseif ($null -ne $Stream) { $Stream.Dispose() }
    }
}
function Get-NormalizedPath([string]$Path) {
    if ([string]::IsNullOrWhiteSpace($Path)) { return $null }
    try { return [System.IO.Path]::GetFullPath($Path) } catch { return $null }
}

function Test-ShapeOfDreamsGameDir([string]$Candidate) {
    $Full = Get-NormalizedPath $Candidate
    if ([string]::IsNullOrWhiteSpace($Full)) { return $null }
    $Managed = Join-Path $Full 'Shape of Dreams_Data\Managed'
    if (-not (Test-Path $Managed -PathType Container)) { return $null }
    if (-not (Test-Path (Join-Path $Managed 'Assembly-CSharp.dll') -PathType Leaf)) { return $null }
    if (-not (Test-Path (Join-Path $Managed 'Dew.Core.dll') -PathType Leaf)) { return $null }
    return $Full
}

function Add-Candidate([System.Collections.IList]$List, [string]$Candidate, [string]$Reason) {
    $Full = Get-NormalizedPath $Candidate
    if ([string]::IsNullOrWhiteSpace($Full)) { return }
    if (-not $List.Contains($Full)) {
        [void]$List.Add($Full)
        Log ('GAME DIR CANDIDATE [' + $Reason + ']: ' + $Full)
    }
}

function Add-SteamLibraryCandidate([System.Collections.IList]$List, [string]$LibraryRoot, [string]$Reason) {
    $Root = Get-NormalizedPath $LibraryRoot
    if ([string]::IsNullOrWhiteSpace($Root)) { return }

    # A library root normally contains steamapps. If a caller already passed steamapps,
    # normalize that shape as well.
    if ((Split-Path -Leaf $Root) -ieq 'steamapps') {
        Add-Candidate $List (Join-Path $Root 'common\Shape of Dreams') ($Reason + '/steamapps')
    }
    else {
        Add-Candidate $List (Join-Path $Root 'steamapps\common\Shape of Dreams') $Reason
    }
}

function Add-LibrariesFromVdf([System.Collections.IList]$List, [string]$SteamRoot, [string]$Reason) {
    $Root = Get-NormalizedPath $SteamRoot
    if ([string]::IsNullOrWhiteSpace($Root)) { return }
    $Vdf = Join-Path $Root 'steamapps\libraryfolders.vdf'
    if (-not (Test-Path $Vdf -PathType Leaf)) { return }

    try {
        foreach ($Line in (Get-Content -LiteralPath $Vdf -ErrorAction Stop)) {
            if ($Line -match '"path"\s+"([^"]+)"') {
                $Library = $Matches[1] -replace '\\\\', '\'
                Add-SteamLibraryCandidate $List $Library ($Reason + '/libraryfolders.vdf')
            }
        }
    }
    catch {
        Log ('WARN: failed to parse Steam libraryfolders.vdf: ' + $_.Exception.Message)
    }
}

function Resolve-ShapeOfDreamsGameDir([string]$ModDir) {
    $Candidates = New-Object System.Collections.ArrayList

    # Classic local mod layout: <Game>\Mods\DariusPrototype.
    Add-Candidate $Candidates (Join-Path $ModDir '..\..') 'legacy-local-layout'

    # Walk upward. This covers both local Mods and Workshop layouts such as
    # <SteamLibrary>\steamapps\workshop\content\2444750\<publishedFileId>.
    try {
        $Current = New-Object System.IO.DirectoryInfo -ArgumentList $ModDir
        for ($i = 0; $i -lt 12 -and $Current -ne $null; $i++) {
            Add-Candidate $Candidates $Current.FullName ('ancestor-' + $i)
            if ($Current.Name -ieq 'steamapps') {
                Add-Candidate $Candidates (Join-Path $Current.FullName 'common\Shape of Dreams') 'same-steam-library'
                Add-LibrariesFromVdf $Candidates $Current.Parent.FullName 'same-steam-root'
            }
            $Current = $Current.Parent
        }
    }
    catch {
        Log ('WARN: failed while inspecting Mod path ancestors: ' + $_.Exception.Message)
    }

    # Explicit override for unusual portable/custom setups.
    Add-Candidate $Candidates $env:SOD_GAME_DIR 'SOD_GAME_DIR'

    # Steam registry locations. These also let us discover other Steam libraries.
    $SteamRoots = New-Object System.Collections.ArrayList
    $RegistryPaths = @(
        @{ Key = 'HKCU:\Software\Valve\Steam'; Name = 'SteamPath' },
        @{ Key = 'HKLM:\SOFTWARE\WOW6432Node\Valve\Steam'; Name = 'InstallPath' },
        @{ Key = 'HKLM:\SOFTWARE\Valve\Steam'; Name = 'InstallPath' }
    )
    foreach ($Entry in $RegistryPaths) {
        try {
            $Value = (Get-ItemProperty -Path $Entry.Key -Name $Entry.Name -ErrorAction Stop).($Entry.Name)
            $Full = Get-NormalizedPath $Value
            if (-not [string]::IsNullOrWhiteSpace($Full) -and -not $SteamRoots.Contains($Full)) {
                [void]$SteamRoots.Add($Full)
            }
        }
        catch { }
    }

    if (${env:ProgramFiles(x86)}) {
        $DefaultSteam = Join-Path ${env:ProgramFiles(x86)} 'Steam'
        $Full = Get-NormalizedPath $DefaultSteam
        if (-not [string]::IsNullOrWhiteSpace($Full) -and -not $SteamRoots.Contains($Full)) {
            [void]$SteamRoots.Add($Full)
        }
    }

    foreach ($SteamRoot in $SteamRoots) {
        Add-SteamLibraryCandidate $Candidates $SteamRoot 'steam-root'
        Add-LibrariesFromVdf $Candidates $SteamRoot 'steam-root'
    }

    foreach ($Candidate in $Candidates) {
        $Valid = Test-ShapeOfDreamsGameDir $Candidate
        if (-not [string]::IsNullOrWhiteSpace($Valid)) {
            Log ('GAME DIR RESOLVED: ' + $Valid)
            return $Valid
        }
    }

    throw ('Could not locate the Shape of Dreams game directory. Mod directory: ' + $ModDir +
           '. Set environment variable SOD_GAME_DIR to the folder containing Shape of Dreams_Data if your Steam library uses an unusual layout.')
}

try {
    Set-Content -Path $LogFile -Value ('Darius v0.30.6-final Mecha VFX visual hotfix 3 build started: ' + (Get-Date -Format 'yyyy-MM-dd HH:mm:ss')) -Encoding UTF8

    Stage 'Locating Shape of Dreams installation'
    $GameDir = Resolve-ShapeOfDreamsGameDir $ProjectDir
    $ManagedDir = Join-Path $GameDir 'Shape of Dreams_Data\Managed'
    Write-Host ('Game directory: ' + $GameDir) -ForegroundColor Green
    Write-Host ('Managed directory: ' + $ManagedDir) -ForegroundColor DarkGreen
    Log ('PROJECT DIR: ' + $ProjectDir)
    Log ('GAME DIR: ' + $GameDir)
    Log ('MANAGED DIR: ' + $ManagedDir)

    Stage 'Checking mod metadata and Steam Workshop identity'
    $MetadataFile = Join-Path $ProjectDir 'about\metadata.json'
    if (-not (Test-Path $MetadataFile)) { throw ('Required mod loader manifest is missing: ' + $MetadataFile) }
    try { $Meta = Get-Content -Path $MetadataFile -Raw | ConvertFrom-Json }
    catch { throw ('Invalid about\metadata.json: ' + $_.Exception.Message) }
    if ([string]::IsNullOrWhiteSpace([string]$Meta.id)) { throw 'metadata.id is missing.' }
    if ([string]::IsNullOrWhiteSpace([string]$Meta.name)) { throw 'metadata.name is missing.' }
    if ($null -eq $Meta.assemblies -or -not (@($Meta.assemblies) -contains 'DariusPrototype.dll')) { throw 'metadata.assemblies must include DariusPrototype.dll.' }
    Write-Host ('Metadata ID: ' + $Meta.id)
    Log ('METADATA OK: id=' + $Meta.id + ' version=' + $Meta.modVer + ' assembly=DariusPrototype.dll')
    $WorkshopIdFile = Join-Path $ProjectDir 'about\publishedfileid.txt'
    if (-not (Test-Path $WorkshopIdFile))
    {
        throw ('Steam Workshop identity file is missing: ' + $WorkshopIdFile)
    }
    $WorkshopId = (Get-Content -Path $WorkshopIdFile -Raw).Trim()
    if ($WorkshopId -ne '3790488345')
    {
        throw ('Unexpected Steam Workshop ID in about\publishedfileid.txt: ' + $WorkshopId)
    }
    Write-Host ('Workshop ID: ' + $WorkshopId)
    Log ('WORKSHOP ID OK: ' + $WorkshopId)

    $DescriptionFile = Join-Path $ProjectDir 'about\description.txt'
    if (Test-Path $DescriptionFile -PathType Leaf) {
        $DescriptionBytes = (Get-Item $DescriptionFile).Length
        if ($DescriptionBytes -gt 8000) { throw ('Steam Workshop description exceeds the 8000-byte limit: ' + $DescriptionBytes + ' bytes') }
        if ($DescriptionBytes -gt 7500) { Write-Warning ('Workshop description is close to the Steam limit: ' + $DescriptionBytes + ' bytes') }
        Log ('WORKSHOP DESCRIPTION BYTES OK: ' + $DescriptionBytes + '/8000')
    }

    Stage 'Checking game references'
    $Required = @(
        'mscorlib.dll','netstandard.dll','System.dll','System.Core.dll','0Harmony.dll',
        'Assembly-CSharp.dll','Dew.Core.dll','Dew.Contents.dll','Dew.External.dll','Dew.UI.dll',
        'Mirror.dll','Newtonsoft.Json.dll','Sirenix.Serialization.dll','UnityEngine.CoreModule.dll',
        'UnityEngine.IMGUIModule.dll','UnityEngine.ParticleSystemModule.dll','UnityEngine.PhysicsModule.dll','UnityEngine.InputLegacyModule.dll',
        'UnityEngine.ImageConversionModule.dll','UnityEngine.AudioModule.dll','UnityEngine.UnityWebRequestModule.dll','UnityEngine.UnityWebRequestAudioModule.dll','UnityEngine.AnimationModule.dll'
    )
    foreach ($Name in $Required) {
        $Path = Join-Path $ManagedDir $Name
        if (-not (Test-Path $Path)) { throw ('Required DLL missing: ' + $Path) }
        Log ('REF OK: ' + $Name)
    }

    Stage 'Preparing Pass2 authentic Riot audio'
    $Pass2RawManifest = Join-Path $ProjectDir 'assets\raw_lol_audio\PASS2_MEDIA_MANIFEST.json'
    $Pass2Ready = Join-Path $ProjectDir 'assets\audio\PASS2_MEDIA_READY.txt'
    if (Test-Path -LiteralPath $Pass2RawManifest -PathType Leaf) {
        $ExpectedRawWem = @(Get-ChildItem -LiteralPath (Split-Path -Parent $Pass2RawManifest) -Recurse -File -Filter '*.wem').Count
        $CurrentPass2Wav = @(Get-ChildItem -LiteralPath (Join-Path $ProjectDir 'assets\audio') -File -Filter 'vo_*full_*.wav' -ErrorAction SilentlyContinue).Count +
            @(Get-ChildItem -LiteralPath (Join-Path $ProjectDir 'assets\audio') -File -Filter 'vo_dunkmaster_*.wav' -ErrorAction SilentlyContinue).Count +
            @(Get-ChildItem -LiteralPath (Join-Path $ProjectDir 'assets\audio') -File -Filter 'vo_mecha_*.wav' -ErrorAction SilentlyContinue).Count +
            @(Get-ChildItem -LiteralPath (Join-Path $ProjectDir 'assets\audio') -File -Filter 'lol_dunkmaster_*.wav' -ErrorAction SilentlyContinue).Count +
            @(Get-ChildItem -LiteralPath (Join-Path $ProjectDir 'assets\audio') -File -Filter 'lol_mecha_*.wav' -ErrorAction SilentlyContinue).Count
        if (-not (Test-Path -LiteralPath $Pass2Ready -PathType Leaf) -or $CurrentPass2Wav -lt $ExpectedRawWem) {
            Log ('PASS2 MEDIA PREP required rawWem=' + $ExpectedRawWem + ' preparedWav=' + $CurrentPass2Wav)
            $Prep = Join-Path $ProjectDir 'Tools\PrepareDariusPass2Media.ps1'
            if (-not (Test-Path -LiteralPath $Prep -PathType Leaf)) { throw ('Pass2 media preparation script missing: ' + $Prep) }
            $PrepOutput = & powershell.exe -NoLogo -NoProfile -ExecutionPolicy Bypass -File $Prep 2>&1
            $PrepExit = $LASTEXITCODE
            foreach ($PrepLine in $PrepOutput) { Log ('PASS2 PREP: ' + [string]$PrepLine) }
            if ($PrepExit -ne 0) { throw ('Pass2 authentic Riot audio preparation failed exit=' + $PrepExit + '. See BUILD_LOG.txt PASS2 PREP lines.') }
        }
        Log ('PASS2 MEDIA READY marker=' + $Pass2Ready)
    }

    Stage 'Checking packaged League assets'
    $RequiredIcons = @(
        'Darius_Champion.png','Darius_Skin_Default.png','Darius_Skin_GodKing.png','Darius_Skin_Dunkmaster.png','Darius_Skin_Mecha.png',
        'Darius_Q_Decimate.png','Darius_W_CripplingStrike.png','Darius_E_Apprehend.png',
        'Darius_R_NoxianGuillotine.png','Darius_P_Hemorrhage.png','Darius_M_Flash.png','Darius_M_Ghost.png',
        'Rune_Conqueror.png','Rune_Triumph.png','Rune_Alacrity.png','Rune_LastStand.png','Rune_AxiomArcanist.png',
        'Rune_SecondWind.png','Rune_Overgrowth.png','Rune_Revitalize.png','Rune_Conditioning.png','Rune_Unflinching.png',
        'Rune_FervorOfBattle.png','Rune_NimbusCloak.png','Rune_Celerity.png','Rune_GatheringStorm.png','Rune_CosmicInsight.png',
        'Item_TrinityForce.png','Item_BlackCleaver.png','Item_SpearOfShojin.png','Item_SteraksGage.png','Item_DeathsDance.png',
        'Item_OverlordsBloodmail.png','Item_SunderedSky.png','Item_Stridebreaker.png','Item_DeadMansPlate.png','Item_YoumuusGhostblade.png','star_awoo.png'
    )
    foreach ($Name in $RequiredIcons) {
        $Path = Join-Path $IconDir $Name
        if (-not (Test-Path $Path)) { throw ('Packaged League icon missing: ' + $Path) }
        $Size = (Get-Item $Path).Length
        if ($Size -le 512) { throw ('Packaged League icon is unexpectedly small/corrupt: ' + $Path + ' bytes=' + $Size) }
        Log ('ASSET OK ICON: ' + $Name + ' bytes=' + $Size)
    }

    $FlashOgg = Join-Path $AudioDir 'flash.ogg'
    if (-not (Test-Path $FlashOgg)) { throw ('Packaged League Flash SFX missing: ' + $FlashOgg) }
    $Bytes = [System.IO.File]::ReadAllBytes($FlashOgg)
    if ($Bytes.Length -lt 2048 -or $Bytes[0] -ne 0x4F -or $Bytes[1] -ne 0x67 -or $Bytes[2] -ne 0x67 -or $Bytes[3] -ne 0x53) {
        throw ('Packaged Flash SFX is not a valid Ogg stream: ' + $FlashOgg)
    }
    Log ('ASSET OK AUDIO: flash.ogg bytes=' + $Bytes.Length)

    $LeagueSfx = @(Get-ChildItem -LiteralPath $AudioDir -File -Filter 'lol_*.wav')
    $LeagueVo = @(Get-ChildItem -LiteralPath $AudioDir -File -Filter 'vo_*.wav')
    $LeagueAudio = @($LeagueSfx + $LeagueVo)

    # Final hotfix: validate the Pass2 payload against the actual packaged Riot WEM set instead
    # of a stale hard-coded VO total. The current source contains 368 Pass2 VO WEMs plus the
    # pre-existing Classic/God-King VO pool (43 WAVs) = 411 VO WAVs. The old 413 check made a
    # completely decoded package fail after media preparation succeeded.
    $Pass2RawFiles = @(Get-ChildItem -LiteralPath (Join-Path $ProjectDir 'assets\raw_lol_audio') -Recurse -File -Filter '*.wem' -ErrorAction SilentlyContinue)
    $MissingDecoded = @()
    foreach ($Raw in $Pass2RawFiles) {
        $Decoded = Join-Path $AudioDir ($Raw.BaseName + '.wav')
        if (-not (Test-Path -LiteralPath $Decoded -PathType Leaf) -or (Get-Item $Decoded).Length -le 44) { $MissingDecoded += $Raw.FullName }
    }
    if ($MissingDecoded.Count -gt 0) {
        throw ('Pass2 Riot media decode is incomplete. Missing/corrupt WAV count=' + $MissingDecoded.Count + ' first=' + $MissingDecoded[0])
    }
    $Pass2VoExpected = @($Pass2RawFiles | Where-Object { $_.BaseName -like 'vo_*' }).Count
    $Pass2SfxExpected = @($Pass2RawFiles | Where-Object { $_.BaseName -like 'lol_*' }).Count
    $LegacyVoCount = $LeagueVo.Count - $Pass2VoExpected
    $LegacySfxCount = $LeagueSfx.Count - $Pass2SfxExpected
    if ($LegacyVoCount -lt 40) { throw ('Pre-Pass2 authentic League VO pool is incomplete. Expected at least 40, found ' + $LegacyVoCount) }
    if ($LegacySfxCount -lt 55) { throw ('Pre-Pass2 authentic League skill-SFX pool is incomplete. Expected at least 55, found ' + $LegacySfxCount) }
    Log ('ASSET COUNT CONTRACT: pass2Raw=' + $Pass2RawFiles.Count + ' pass2VO=' + $Pass2VoExpected + ' pass2SFX=' + $Pass2SfxExpected +
        ' legacyVO=' + $LegacyVoCount + ' legacySFX=' + $LegacySfxCount + ' finalVO=' + $LeagueVo.Count + ' finalSFX=' + $LeagueSfx.Count)
    foreach ($AudioFile in $LeagueAudio) {
        if ($AudioFile.Length -lt 48) { throw ('Authentic League audio file is unexpectedly small: ' + $AudioFile.FullName) }
        $Header = New-Object byte[] 12
        $Fs = [System.IO.File]::Open($AudioFile.FullName, [System.IO.FileMode]::Open, [System.IO.FileAccess]::Read, [System.IO.FileShare]::Read)
        try { [void]$Fs.Read($Header, 0, 12) } finally { $Fs.Dispose() }
        if ($Header[0] -ne 0x52 -or $Header[1] -ne 0x49 -or $Header[2] -ne 0x46 -or $Header[3] -ne 0x46 -or
            $Header[8] -ne 0x57 -or $Header[9] -ne 0x41 -or $Header[10] -ne 0x56 -or $Header[11] -ne 0x45) {
            throw ('Authentic League audio is not RIFF/WAVE: ' + $AudioFile.FullName)
        }
    }
    Log ('ASSET OK LOL AUDIO: SFX=' + $LeagueSfx.Count + ' VO=' + $LeagueVo.Count + ' total=' + $LeagueAudio.Count)

    $VfxDir = Join-Path $ProjectDir 'assets\vfx'
    # Q/W/E/R no longer depend on the old hand-authored VFX mask bank. Only the two
    # God-King persistent Noxian-Might compatibility textures remain in this folder.
    $RetainedLegacyVfx = @('gk_glow.png','gk_wisps_red.png')
    foreach ($Name in $RetainedLegacyVfx) {
        $Path = Join-Path $VfxDir $Name
        if (-not (Test-Path $Path)) { throw ('Retained compatibility VFX texture missing: ' + $Path) }
        if ((Get-Item $Path).Length -lt 128) { throw ('Retained compatibility VFX texture is unexpectedly small: ' + $Path) }
        Log ('ASSET OK RETAINED COMPAT VFX: ' + $Name + ' bytes=' + (Get-Item $Path).Length)
    }


    # v0.24: direct Riot BIN/TEX/SCB conversion payload. These are not hand-authored replacement
    # textures: the JSON contains authored VfxSystemDefinitionData emitter parameters and points to
    # the exact converted LoL textures/static meshes extracted from the user's Darius WAD.
    $LolVfxDir = Join-Path $ProjectDir 'assets\lol_vfx'
    $LolVfxManifest = Join-Path $LolVfxDir 'darius_lol_vfx.json'
    if (-not (Test-Path $LolVfxManifest)) { throw ('Converted Riot VFX manifest missing: ' + $LolVfxManifest) }
    try { $LolVfx = Get-Content -Path $LolVfxManifest -Raw | ConvertFrom-Json }
    catch { throw ('Converted Riot VFX manifest is invalid JSON: ' + $_.Exception.Message) }
    $LolSystems = @($LolVfx.systems.PSObject.Properties).Count
    $LolTextures = @(Get-ChildItem -LiteralPath (Join-Path $LolVfxDir 'textures') -File -Filter '*.png').Count
    $LolMeshes = @(Get-ChildItem -LiteralPath (Join-Path $LolVfxDir 'meshes') -File -Filter '*.json').Count
    $Pass5Systems = @($LolVfx.pass5ConvertedSystems).Count
    $AssetErrors = @($LolVfx.assetErrors).Count
    if ($LolSystems -ne 164) { throw ('Expected 164 final Riot Darius VFX systems, found ' + $LolSystems) }
    if ($Pass5Systems -ne 117) { throw ('Expected 117 Dunkmaster/Mecha Pass5 systems, found ' + $Pass5Systems) }
    if ($LolTextures -lt 113) { throw ('Expected at least 113 converted Riot PNG textures, found ' + $LolTextures) }
    if ($LolMeshes -lt 51) { throw ('Expected at least 51 converted Riot VFX meshes, found ' + $LolMeshes) }
    if ($AssetErrors -ne 0) { throw ('Final Riot VFX manifest still contains asset errors: ' + $AssetErrors) }
    foreach ($AssetProp in @($LolVfx.assets.PSObject.Properties)) {
        $Rel = [string]$AssetProp.Value.file
        if ([string]::IsNullOrWhiteSpace($Rel)) { continue }
        $Resolved = [System.IO.Path]::GetFullPath((Join-Path $LolVfxDir ($Rel -replace '/', [System.IO.Path]::DirectorySeparatorChar)))
        if (-not (Test-Path -LiteralPath $Resolved -PathType Leaf)) { throw ('Riot VFX referenced asset missing: ' + $Rel + ' -> ' + $Resolved) }
    }
    Log ('ASSET OK RIOT VFX FINAL: systems=' + $LolSystems + ' pass5=' + $Pass5Systems + ' pngTextures=' + $LolTextures + ' meshes=' + $LolMeshes + ' assetErrors=' + $AssetErrors)

    Stage 'Checking dotnet SDK'
    $SystemDotnet = Get-Command dotnet -ErrorAction SilentlyContinue
    if (-not $SystemDotnet) {
        throw '.NET SDK was not found. Install .NET 8 SDK, then run BUILD_DARIUS.bat again. The build script does not download SDKs or game assets.'
    }
    $Dotnet = $SystemDotnet.Source
    $SdkList = & $Dotnet --list-sdks 2>$null
    if ($LASTEXITCODE -ne 0 -or -not $SdkList) {
        throw '.NET SDK was not detected. Install .NET 8 SDK, then run BUILD_DARIUS.bat again.'
    }
    Log ('DOTNET: ' + $Dotnet)
    Log ('SDK LIST: ' + (($SdkList | ForEach-Object { [string]$_ }) -join '; '))

    Stage 'Building CSharp mod'
    $ProjectFile = Join-Path $ProjectDir 'DariusPrototype.csproj'
    if (-not (Test-Path $ProjectFile)) { throw ('Project file not found: ' + $ProjectFile) }
    $RootDll = Join-Path $ProjectDir 'DariusPrototype.dll'
    $RootPdb = Join-Path $ProjectDir 'DariusPrototype.pdb'
    Remove-Item $RootDll,$RootPdb -Force -ErrorAction SilentlyContinue
    Remove-Item (Join-Path $ProjectDir 'bin'),(Join-Path $ProjectDir 'obj') -Recurse -Force -ErrorAction SilentlyContinue
    Log 'SOURCE BUILD STAMP: v0.30.6-final mecha-vfx-visual-hotfix3'

    $BuildArgs = @('build',$ProjectFile,'-c','Release',('-p:GameDir=' + $GameDir),('-p:GameManagedDir=' + $ManagedDir),'--nologo')
    Log ('BUILD COMMAND: ' + $Dotnet + ' ' + ($BuildArgs -join ' '))
    $Output = & $Dotnet @BuildArgs 2>&1
    $Exit = $LASTEXITCODE
    foreach ($Line in $Output) { Write-Host ([string]$Line); Log ([string]$Line) }
    if ($Exit -ne 0) { throw ('CSharp build failed. Exit code: ' + $Exit) }

    $BuiltDll = Join-Path $ProjectDir 'bin\Release\netstandard2.1\DariusPrototype.dll'
    if (-not (Test-Path $BuiltDll)) { throw 'Build succeeded but DariusPrototype.dll was not generated.' }
    Copy-Item $BuiltDll $RootDll -Force
    $DllHash = (Get-FileHash -Algorithm SHA256 -Path $RootDll).Hash
    Log ('OUTPUT DLL SHA256: ' + $DllHash)
    Write-Host ('Build stamp: v0.30.6-final mecha-vfx-visual-hotfix3') -ForegroundColor Green
    Write-Host ('DLL SHA256: ' + $DllHash) -ForegroundColor DarkGreen
    $BuiltPdb = Join-Path $ProjectDir 'bin\Release\netstandard2.1\DariusPrototype.pdb'
    if (Test-Path $BuiltPdb) { Copy-Item $BuiltPdb $RootPdb -Force }

    $ModelPaths = @(
        (Join-Path $ProjectDir 'assets\models\darius.glb'),
        (Join-Path $ProjectDir 'assets\models\darius_godking.glb'),
        (Join-Path $ProjectDir 'assets\models\darius_dunkmaster.glb'),
        (Join-Path $ProjectDir 'assets\models\darius_mecha.glb')
    )
    Assert-GlbIntegrity $ModelPaths[0] 'Skin_Darius_Default -> darius.glb (经典德莱厄斯)' 1 89 22 @('Idle1','Run','Death','Attack1','Attack2','Crit','Darius_Spell1_IN.anm','Spell1','Spell2','Spell3','Spell4')
    Assert-GlbIntegrity $ModelPaths[1] 'Skin_Darius_GodKing -> darius_godking.glb (神王德莱厄斯)' 5 179 48 @('Idle1_Base','Run_Normal','Death','Attack1','Attack2','Crit','Darius_Skin15_Spell1_IN.anm','Spell1','Spell2','Spell3','Spell4')
    Assert-GlbIntegrity $ModelPaths[2] 'Skin_Darius_Dunkmaster -> darius_dunkmaster.glb (灌篮高手 德莱厄斯)' 1 99 36 @('Idle1_Base','Darius_Skin04_Run.anm','Death','Attack1','Attack2','Crit','Darius_Skin04_Spell1_IN.anm','Spell1','Spell2','Spell3','Darius_Skin04_Spell4_A.anm')
    Assert-GlbIntegrity $ModelPaths[3] 'Skin_Darius_Mecha -> darius_mecha.glb (机神 德莱厄斯)' 13 246 59 @('Idle1_Base','Run_Normal','Death','Attack1','Attack2','Crit','Spell1_IN_Stand.SKINS_Darius_Skin67.anm','Spell1','Spell2','Spell3','Spell4')

    $AnimManifest = Join-Path $ProjectDir 'assets\animations\manifest.json'
    $RetargetProfile = Join-Path $ProjectDir 'assets\retarget\darius_humanoid_profile.json'
    if (-not (Test-Path $AnimManifest)) { throw ('Animation manifest missing: ' + $AnimManifest) }
    if (-not (Test-Path $RetargetProfile)) { throw ('Retarget profile missing: ' + $RetargetProfile) }
    $AnimCount = (Get-ChildItem (Join-Path $ProjectDir 'assets\animations') -Filter '*.sodanim.json' -File).Count
    if ($AnimCount -lt 22) { throw ('Expected 22 Darius animation clips, found: ' + $AnimCount) }
    Log ('ANIMATION OK: clips=' + $AnimCount + ' manifest=' + $AnimManifest)
    Log ('RETARGET OK: ' + $RetargetProfile)

    Stage 'BUILD COMPLETE'
    Write-Host ('DLL: ' + $RootDll) -ForegroundColor Green
    Write-Host 'Independent Hero_Darius build is ready in this Mod folder.' -ForegroundColor Green
    Write-Host ('Build log: ' + $LogFile)
    exit 0
}
catch {
    try { Log ('ERROR: ' + $_.Exception.Message) } catch { }
    Write-Host ''
    Write-Host ('BUILD FAILED: ' + $_.Exception.Message) -ForegroundColor Red
    Write-Host ('See: ' + $LogFile) -ForegroundColor Yellow
    exit 1
}
