param(
    [Parameter(Mandatory = $true)]
    [string]$UnityExe,

    [Parameter(Mandatory = $true)]
    [string]$BlenderExe,

    [string]$RepoRoot = (Resolve-Path (Join-Path $PSScriptRoot "..\..")).Path,

    [switch]$KeepTemp
)

$ErrorActionPreference = "Stop"
Set-StrictMode -Version Latest

function Invoke-Checked {
    param([string]$Exe, [string[]]$Arguments)
    Write-Host "> $Exe $($Arguments -join ' ')"
    & $Exe @Arguments
    if ($LASTEXITCODE -ne 0) {
        throw "Command failed with exit code ${LASTEXITCODE}: $Exe"
    }
}

function Format-ProcessArgument {
    param([string]$Value)
    if ($null -eq $Value) { return '""' }
    if ($Value -notmatch '[\s"]') { return $Value }
    return '"' + ($Value.Replace('"', '\"')) + '"'
}

function Write-UnityLogDelta {
    param(
        [string]$Path,
        [ref]$Offset
    )

    if (-not (Test-Path $Path -PathType Leaf)) { return }

    $stream = $null
    try {
        $stream = [System.IO.File]::Open(
            $Path,
            [System.IO.FileMode]::Open,
            [System.IO.FileAccess]::Read,
            [System.IO.FileShare]::ReadWrite
        )
        if ($Offset.Value -gt $stream.Length) { $Offset.Value = [int64]0 }
        $remaining = $stream.Length - $Offset.Value
        if ($remaining -le 0) { return }

        [void]$stream.Seek($Offset.Value, [System.IO.SeekOrigin]::Begin)
        $buffer = New-Object byte[] ([int]$remaining)
        $read = $stream.Read($buffer, 0, $buffer.Length)
        if ($read -gt 0) {
            $Offset.Value += $read
            Write-Host -NoNewline ([System.Text.Encoding]::UTF8.GetString($buffer, 0, $read))
        }
    }
    catch {
        # The live tail is diagnostic only. The Unity process exit code remains authoritative.
    }
    finally {
        if ($null -ne $stream) { $stream.Dispose() }
    }
}

function Invoke-UnityChecked {
    param(
        [string]$Exe,
        [string[]]$Arguments,
        [string]$LogPath
    )

    $allArguments = @($Arguments) + @("-logFile", $LogPath)
    Write-Host "> $Exe $($allArguments -join ' ')"

    # Unity.exe is a Windows GUI-subsystem process. Start it explicitly, then tail its log with
    # FileShare.ReadWrite while it runs. This gives PowerShell/Agent callers a real wait boundary
    # and visible progress even though Unity keeps the log file open.
    $argumentLine = ($allArguments | ForEach-Object { Format-ProcessArgument $_ }) -join ' '
    $process = Start-Process -FilePath $Exe -ArgumentList $argumentLine -PassThru
    [int64]$offset = 0
    while (-not $process.HasExited) {
        Write-UnityLogDelta $LogPath ([ref]$offset)
        Start-Sleep -Milliseconds 500
        $process.Refresh()
    }
    $process.WaitForExit()
    Write-UnityLogDelta $LogPath ([ref]$offset)

    if ($process.ExitCode -ne 0) {
        throw "Unity command failed with exit code $($process.ExitCode). See: $LogPath"
    }
}

$UnityExe = (Resolve-Path $UnityExe).Path
$BlenderExe = (Resolve-Path $BlenderExe).Path
$RepoRoot = (Resolve-Path $RepoRoot).Path
$modelRoot = Join-Path $RepoRoot "assets\models"
$converter = Join-Path $PSScriptRoot "convert_glb_to_fbx.py"
$editorBuilder = Join-Path $PSScriptRoot "Editor\DariusModelBundleBuilder.cs"

if (-not (Test-Path $modelRoot -PathType Container)) { throw "Model asset directory missing: $modelRoot" }
if (-not (Test-Path $converter -PathType Leaf)) { throw "Converter missing: $converter" }
if (-not (Test-Path $editorBuilder -PathType Leaf)) { throw "Unity builder missing: $editorBuilder" }

$workBase = Join-Path $RepoRoot "build\native-assets-work"
$workName = (Get-Date -Format "yyyyMMdd-HHmmss") + "_" + [Guid]::NewGuid().ToString("N").Substring(0, 8)
$workRoot = Join-Path $workBase $workName
$fbxRoot = Join-Path $workRoot "fbx"
$unityProject = Join-Path $workRoot "UnityProject"
$createLog = Join-Path $workRoot "unity-create-project.log"
$buildLog = Join-Path $workRoot "unity-build-native-models.log"
$preserveWorkspace = [bool]$KeepTemp
New-Item -ItemType Directory -Path $fbxRoot -Force | Out-Null
Write-Host "Native asset workspace: $workRoot"

$models = @(
    @{ Input = "darius.glb"; Output = "darius.fbx" },
    @{ Input = "darius_godking.glb"; Output = "darius_godking.fbx" },
    @{ Input = "darius_dunkmaster.glb"; Output = "darius_dunkmaster.fbx" },
    @{ Input = "darius_mecha.glb"; Output = "darius_mecha.fbx" }
)

try {
    foreach ($model in $models) {
        $src = Join-Path $modelRoot $model.Input
        if (-not (Test-Path $src -PathType Leaf)) { throw "Required GLB missing: $src" }
        $dst = Join-Path $fbxRoot $model.Output
        Invoke-Checked $BlenderExe @(
            "--background",
            "--python", $converter,
            "--",
            $src,
            $dst
        )
    }

    # Create a clean throwaway Unity project under repository build/ so all generated state and
    # diagnostic logs are easy to inspect and remain covered by the repository build ignore rule.
    Invoke-UnityChecked $UnityExe @(
        "-batchmode",
        "-quit",
        "-createProject", $unityProject,
        "-buildTarget", "StandaloneWindows64"
    ) $createLog

    # FBX texture references are relative. Put Blender's exported PNG sidecars beside the FBX
    # destination before Unity imports the models so ModelImporter can bind them normally.
    $textureFiles = @(Get-ChildItem -Path $fbxRoot -Filter "*.png" -File)
    if ($textureFiles.Count -eq 0) { throw "Blender produced no native model texture sidecars in: $fbxRoot" }
    $unitySourceDir = Join-Path $unityProject "Assets\DariusSource"
    New-Item -ItemType Directory -Path $unitySourceDir -Force | Out-Null
    foreach ($texture in $textureFiles) {
        Copy-Item $texture.FullName (Join-Path $unitySourceDir $texture.Name) -Force
    }
    Write-Host "Native model texture sidecars staged for Unity: $($textureFiles.Count)"

    $editorDir = Join-Path $unityProject "Assets\Editor"
    New-Item -ItemType Directory -Path $editorDir -Force | Out-Null
    Copy-Item $editorBuilder (Join-Path $editorDir "DariusModelBundleBuilder.cs") -Force

    $oldRepo = $env:DARIUS_REPO_ROOT
    $oldFbx = $env:DARIUS_MODEL_FBX_DIR
    $oldWork = $env:DARIUS_NATIVE_WORK_ROOT
    try {
        $env:DARIUS_REPO_ROOT = $RepoRoot
        $env:DARIUS_MODEL_FBX_DIR = $fbxRoot
        $env:DARIUS_NATIVE_WORK_ROOT = $workRoot
        Invoke-UnityChecked $UnityExe @(
            "-batchmode",
            "-projectPath", $unityProject,
            "-buildTarget", "StandaloneWindows64",
            "-executeMethod", "DariusModelBundleBuilder.BuildAllBatch"
        ) $buildLog
    }
    finally {
        $env:DARIUS_REPO_ROOT = $oldRepo
        $env:DARIUS_MODEL_FBX_DIR = $oldFbx
        $env:DARIUS_NATIVE_WORK_ROOT = $oldWork
    }

    $bundle = Join-Path $modelRoot "darius_models.bundle"
    if (-not (Test-Path $bundle -PathType Leaf)) {
        $preserveWorkspace = $true
        throw "Unity exited successfully without producing bundle: $bundle. Workspace preserved: $workRoot"
    }

    Write-Host "Native model bundle ready: $bundle"
    Write-Host "Size: $((Get-Item $bundle).Length) bytes"
}
catch {
    $preserveWorkspace = $true
    throw
}
finally {
    if ($preserveWorkspace) {
        Write-Host "Keeping native asset workspace: $workRoot"
        if (Test-Path $createLog -PathType Leaf) { Write-Host "Unity project log: $createLog" }
        if (Test-Path $buildLog -PathType Leaf) { Write-Host "Unity build log: $buildLog" }
    }
    elseif (Test-Path $workRoot) {
        Remove-Item $workRoot -Recurse -Force
    }
}
