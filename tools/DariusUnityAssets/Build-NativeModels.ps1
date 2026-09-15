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

function Show-LogTail {
    param([string]$Path, [int]$Lines = 120)
    if (-not (Test-Path $Path -PathType Leaf)) { return }
    Write-Host "----- Unity log tail: $Path -----" -ForegroundColor Yellow
    Get-Content $Path -Tail $Lines | ForEach-Object { Write-Host $_ }
    Write-Host "----- end Unity log tail -----" -ForegroundColor Yellow
}

function Invoke-UnityChecked {
    param(
        [string]$Exe,
        [string[]]$Arguments,
        [string]$LogPath
    )

    $allArguments = @($Arguments) + @("-logFile", $LogPath)
    Write-Host "> $Exe $($allArguments -join ' ')"

    # Unity.exe is a Windows GUI-subsystem process. PowerShell's direct invocation is not a
    # reliable synchronization boundary for it, so explicitly wait for the editor process.
    # This also gives Agent/CI callers the real Unity exit code instead of racing the bundle check.
    $argumentLine = ($allArguments | ForEach-Object { Format-ProcessArgument $_ }) -join ' '
    $process = Start-Process -FilePath $Exe -ArgumentList $argumentLine -Wait -PassThru
    if ($process.ExitCode -ne 0) {
        Show-LogTail $LogPath
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

$workRoot = Join-Path ([System.IO.Path]::GetTempPath()) ("DariusNativeAssets_" + [Guid]::NewGuid().ToString("N"))
$fbxRoot = Join-Path $workRoot "fbx"
$unityProject = Join-Path $workRoot "UnityProject"
$createLog = Join-Path $workRoot "unity-create-project.log"
$buildLog = Join-Path $workRoot "unity-build-native-models.log"
$preserveWorkspace = [bool]$KeepTemp
New-Item -ItemType Directory -Path $fbxRoot -Force | Out-Null

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

    # Create a clean throwaway Unity project so the repository never acquires Library/Temp/editor
    # state and the mod runtime never gains a glTF importer dependency.
    Invoke-UnityChecked $UnityExe @(
        "-batchmode",
        "-quit",
        "-createProject", $unityProject,
        "-buildTarget", "StandaloneWindows64"
    ) $createLog

    $editorDir = Join-Path $unityProject "Assets\Editor"
    New-Item -ItemType Directory -Path $editorDir -Force | Out-Null
    Copy-Item $editorBuilder (Join-Path $editorDir "DariusModelBundleBuilder.cs") -Force

    $oldRepo = $env:DARIUS_REPO_ROOT
    $oldFbx = $env:DARIUS_MODEL_FBX_DIR
    try {
        $env:DARIUS_REPO_ROOT = $RepoRoot
        $env:DARIUS_MODEL_FBX_DIR = $fbxRoot
        Invoke-UnityChecked $UnityExe @(
            "-batchmode",
            "-quit",
            "-projectPath", $unityProject,
            "-buildTarget", "StandaloneWindows64",
            "-executeMethod", "DariusModelBundleBuilder.BuildAll"
        ) $buildLog
    }
    finally {
        $env:DARIUS_REPO_ROOT = $oldRepo
        $env:DARIUS_MODEL_FBX_DIR = $oldFbx
    }

    $bundle = Join-Path $modelRoot "darius_models.bundle"
    if (-not (Test-Path $bundle -PathType Leaf)) {
        $preserveWorkspace = $true
        Show-LogTail $buildLog
        throw "Unity exited successfully without producing bundle: $bundle. Workspace preserved: $workRoot"
    }

    $builderMessages = @(Select-String -Path $buildLog -SimpleMatch -Pattern "[DariusNativeAssets]" -ErrorAction SilentlyContinue)
    foreach ($message in $builderMessages) { Write-Host $message.Line }
    Write-Host "Native model bundle ready: $bundle"
    Write-Host "Size: $((Get-Item $bundle).Length) bytes"
}
catch {
    $preserveWorkspace = $true
    throw
}
finally {
    if ($preserveWorkspace) {
        Write-Host "Keeping temporary build workspace: $workRoot"
        if (Test-Path $createLog -PathType Leaf) { Write-Host "Unity project log: $createLog" }
        if (Test-Path $buildLog -PathType Leaf) { Write-Host "Unity build log: $buildLog" }
    }
    elseif (Test-Path $workRoot) {
        Remove-Item $workRoot -Recurse -Force
    }
}
