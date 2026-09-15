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
    Invoke-Checked $UnityExe @(
        "-batchmode",
        "-quit",
        "-createProject", $unityProject,
        "-logFile", "-"
    )

    $editorDir = Join-Path $unityProject "Assets\Editor"
    New-Item -ItemType Directory -Path $editorDir -Force | Out-Null
    Copy-Item $editorBuilder (Join-Path $editorDir "DariusModelBundleBuilder.cs") -Force

    $oldRepo = $env:DARIUS_REPO_ROOT
    $oldFbx = $env:DARIUS_MODEL_FBX_DIR
    try {
        $env:DARIUS_REPO_ROOT = $RepoRoot
        $env:DARIUS_MODEL_FBX_DIR = $fbxRoot
        Invoke-Checked $UnityExe @(
            "-batchmode",
            "-quit",
            "-projectPath", $unityProject,
            "-executeMethod", "DariusModelBundleBuilder.BuildAll",
            "-logFile", "-"
        )
    }
    finally {
        $env:DARIUS_REPO_ROOT = $oldRepo
        $env:DARIUS_MODEL_FBX_DIR = $oldFbx
    }

    $bundle = Join-Path $modelRoot "darius_models.bundle"
    if (-not (Test-Path $bundle -PathType Leaf)) { throw "Unity completed without producing bundle: $bundle" }
    Write-Host "Native model bundle ready: $bundle"
    Write-Host "Size: $((Get-Item $bundle).Length) bytes"
}
finally {
    if ($KeepTemp) {
        Write-Host "Keeping temporary build workspace: $workRoot"
    }
    elseif (Test-Path $workRoot) {
        Remove-Item $workRoot -Recurse -Force
    }
}
