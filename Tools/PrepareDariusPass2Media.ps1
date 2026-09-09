$ErrorActionPreference = 'Stop'
$ProgressPreference = 'SilentlyContinue'

$ProjectDir = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
$RawDir = Join-Path $ProjectDir 'assets\raw_lol_audio'
$AudioDir = Join-Path $ProjectDir 'assets\audio'
$Marker = Join-Path $AudioDir 'PASS2_MEDIA_READY.txt'
$LogDir = Join-Path ([IO.Path]::GetTempPath()) 'ShapeOfDreams_DariusBuild'
$LogFile = Join-Path $LogDir 'DariusPass2MediaPrepare.log'
New-Item -ItemType Directory -Path $AudioDir,$LogDir -Force | Out-Null

function Log([string]$m) {
    $line = ('{0:yyyy-MM-dd HH:mm:ss.fff} {1}' -f (Get-Date),$m)
    Write-Host $line
    Add-Content -LiteralPath $LogFile -Value $line -Encoding UTF8
}

function Find-Vgmstream {
    $cmd = Get-Command 'vgmstream-cli.exe' -ErrorAction SilentlyContinue
    if ($cmd -and $cmd.Source) { return $cmd.Source }
    $known = @(
        (Join-Path $PSScriptRoot 'vgmstream\vgmstream-cli.exe'),
        (Join-Path $env:LOCALAPPDATA 'LeagueToolkit\vgmstream\vgmstream-cli.exe'),
        (Join-Path $env:LOCALAPPDATA 'vgmstream\vgmstream-cli.exe')
    )
    foreach ($p in $known) { if ($p -and (Test-Path -LiteralPath $p -PathType Leaf)) { return $p } }
    return $null
}

function Install-Vgmstream {
    $install = Join-Path $PSScriptRoot 'vgmstream'
    New-Item -ItemType Directory -Path $install -Force | Out-Null
    Log 'vgmstream-cli not found; resolving latest official vgmstream Windows x64 release.'
    $release = Invoke-RestMethod -UseBasicParsing -Uri 'https://api.github.com/repos/vgmstream/vgmstream/releases/latest' -Headers @{ 'User-Agent'='ShapeOfDreams-Darius-Pass2' }
    $asset = @($release.assets | Where-Object { $_.name -ieq 'vgmstream-win64.zip' } | Select-Object -First 1)
    if (-not $asset) { $asset = @($release.assets | Where-Object { $_.name -like 'vgmstream-win*.zip' } | Select-Object -First 1) }
    if (-not $asset) { throw 'Official vgmstream Windows archive was not found in the latest GitHub release.' }
    $zip = Join-Path $env:TEMP ('vgmstream_' + [Guid]::NewGuid().ToString('N') + '.zip')
    Log ('Downloading ' + $asset.browser_download_url)
    Invoke-WebRequest -UseBasicParsing -Uri $asset.browser_download_url -OutFile $zip -Headers @{ 'User-Agent'='ShapeOfDreams-Darius-Pass2' }
    try {
        Remove-Item -LiteralPath (Join-Path $install '*') -Recurse -Force -ErrorAction SilentlyContinue
        Expand-Archive -LiteralPath $zip -DestinationPath $install -Force
    } finally { Remove-Item -LiteralPath $zip -Force -ErrorAction SilentlyContinue }
    $exe = Get-ChildItem -LiteralPath $install -Recurse -File -Filter 'vgmstream-cli.exe' | Select-Object -First 1
    if (-not $exe) { throw 'vgmstream archive extracted but vgmstream-cli.exe was not found.' }
    return $exe.FullName
}

try {
    Set-Content -LiteralPath $LogFile -Value ('Darius Pass2 media prep ' + (Get-Date)) -Encoding UTF8
    $manifest = Join-Path $RawDir 'PASS2_MEDIA_MANIFEST.json'
    if (-not (Test-Path -LiteralPath $manifest -PathType Leaf)) { throw ('Raw Riot media manifest missing: ' + $manifest) }
    $wems = @(Get-ChildItem -LiteralPath $RawDir -Recurse -File -Filter '*.wem')
    if ($wems.Count -lt 400) { throw ('Expected the Pass2 Riot WEM source set; found only ' + $wems.Count) }
    $vgm = Find-Vgmstream
    if (-not $vgm) { $vgm = Install-Vgmstream }
    Log ('vgmstream-cli=' + $vgm)
    $done = 0; $skipped = 0
    foreach ($wem in $wems) {
        $wav = Join-Path $AudioDir ($wem.BaseName + '.wav')
        if ((Test-Path -LiteralPath $wav -PathType Leaf) -and (Get-Item $wav).Length -gt 44 -and (Get-Item $wav).LastWriteTimeUtc -ge $wem.LastWriteTimeUtc) {
            $skipped++; continue
        }
        $output = & $vgm '-o' $wav $wem.FullName 2>&1
        if ($LASTEXITCODE -ne 0 -or -not (Test-Path -LiteralPath $wav -PathType Leaf) -or (Get-Item $wav).Length -le 44) {
            throw ('vgmstream decode failed for ' + $wem.FullName + ': ' + (($output | ForEach-Object { [string]$_ }) -join ' '))
        }
        $done++
        if (($done % 40) -eq 0) { Log ('Decoded ' + $done + '/' + $wems.Count) }
    }
    $newSfx = @(Get-ChildItem -LiteralPath $AudioDir -File -Filter 'lol_dunkmaster_*.wav').Count + @(Get-ChildItem -LiteralPath $AudioDir -File -Filter 'lol_mecha_*.wav').Count
    $fullVo = @(Get-ChildItem -LiteralPath $AudioDir -File -Filter 'vo_*full_*.wav').Count + @(Get-ChildItem -LiteralPath $AudioDir -File -Filter 'vo_dunkmaster_*.wav').Count + @(Get-ChildItem -LiteralPath $AudioDir -File -Filter 'vo_mecha_*.wav').Count
    if ($newSfx -lt 60) { throw ('Pass2 skin SFX decode count too low: ' + $newSfx) }
    if ($fullVo -lt 350) { throw ('Pass2 full VO decode count too low: ' + $fullVo) }
    $text = @(
        'Darius Pass2 authentic Riot Wwise media READY',
        ('prepared=' + (Get-Date -Format o)),
        ('rawWem=' + $wems.Count),
        ('decodedNow=' + $done),
        ('skippedExisting=' + $skipped),
        ('pass2SkinSfx=' + $newSfx),
        ('pass2FullVo=' + $fullVo),
        ('vgmstream=' + $vgm)
    )
    [IO.File]::WriteAllLines($Marker,$text,(New-Object Text.UTF8Encoding($true)))
    Log ('READY marker=' + $Marker + ' SFX=' + $newSfx + ' VO=' + $fullVo)
    exit 0
}
catch {
    Log ('FAILED: ' + $_.Exception.Message)
    Write-Host ('Media preparation failed. Log: ' + $LogFile) -ForegroundColor Red
    exit 1
}
