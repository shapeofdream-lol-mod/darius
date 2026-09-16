param(
    [Parameter(Mandatory = $true)][string]$UnityExe,
    [Parameter(Mandatory = $true)][string]$BlenderExe,
    [string]$RepoRoot = (Resolve-Path (Join-Path $PSScriptRoot "..\..")).Path,
    [switch]$KeepTemp
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest
. (Join-Path $PSScriptRoot 'Native-Model-Contract.ps1')

function Invoke-Checked {
    param([string]$Exe, [string[]]$Arguments, [string]$Label)
    Write-Host "> $Label"
    & $Exe @Arguments
    if ($LASTEXITCODE -ne 0) { throw "$Label failed with exit code $LASTEXITCODE" }
}

$UnityExe = (Resolve-Path $UnityExe).Path
$BlenderExe = (Resolve-Path $BlenderExe).Path
$RepoRoot = (Resolve-Path $RepoRoot).Path
$modelRoot = Join-Path $RepoRoot 'assets\models'
$converter = Join-Path $PSScriptRoot 'convert_glb_to_fbx.py'
$editorSourceDir = Join-Path $PSScriptRoot 'Editor'
$sharedProfile = Join-Path $RepoRoot 'src\DariusPrototype\Native\DariusNativeSkinProfiles.cs'
$editorFiles = @(
    'DariusModelBundleBuilder.cs', 'DariusModelImportUtility.cs', 'DariusModelMaterialBinder.cs',
    'DariusModelMeshProcessor.cs', 'DariusModelOverlayMeshBuilder.cs', 'DariusModelPrefabBuilder.cs',
    'DariusModelContractValidator.cs'
)

foreach ($required in @($modelRoot, $converter, $sharedProfile)) {
    if (-not (Test-Path $required)) { throw "Native asset input missing: $required" }
}
foreach ($name in $editorFiles) {
    $path = Join-Path $editorSourceDir $name
    if (-not (Test-Path $path -PathType Leaf)) { throw "Unity editor pipeline file missing: $path" }
}
$models = @(Get-DariusNativeProfiles -ProfilePath $sharedProfile)

$workRoot = Join-Path (Join-Path $RepoRoot 'build\native-assets-work') ((Get-Date -Format 'yyyyMMdd-HHmmss') + '_' + [Guid]::NewGuid().ToString('N').Substring(0, 8))
$fbxRoot = Join-Path $workRoot 'fbx'
$unityProject = Join-Path $workRoot 'UnityProject'
$createLog = Join-Path $workRoot 'unity-create-project.log'
$buildLog = Join-Path $workRoot 'unity-build-native-models.log'
$preserveWorkspace = [bool]$KeepTemp
New-Item -ItemType Directory -Path $fbxRoot -Force | Out-Null
Write-Host "Native asset workspace: $workRoot"

try {
    foreach ($model in $models) {
        $src = Join-Path $modelRoot $model.Input
        if (-not (Test-Path $src -PathType Leaf)) { throw "Required GLB missing: $src" }
        $dst = Join-Path $fbxRoot $model.Output
        Invoke-Checked $BlenderExe @('--background', '--python', $converter, '--', $src, $dst) "Blender $($model.Variant)"
    }

    Invoke-Checked $UnityExe @('-batchmode', '-quit', '-createProject', $unityProject, '-buildTarget', 'StandaloneWindows64', '-logFile', $createLog) 'Unity project creation'
    $textures = @(Get-ChildItem $fbxRoot -Filter '*.png' -File)
    $maps = @(Get-ChildItem $fbxRoot -Filter '*__materials.tsv' -File)
    if ($textures.Count -eq 0) { throw "Blender produced no texture sidecars: $fbxRoot" }
    if ($maps.Count -ne $models.Count) { throw "Material map count mismatch expected=$($models.Count) actual=$($maps.Count)" }

    $sourceDir = Join-Path $unityProject 'Assets\DariusSource'
    $editorDir = Join-Path $unityProject 'Assets\Editor'
    New-Item -ItemType Directory -Path $sourceDir, $editorDir -Force | Out-Null
    foreach ($sidecar in @($textures) + @($maps)) { Copy-Item $sidecar.FullName (Join-Path $sourceDir $sidecar.Name) -Force }
    foreach ($name in $editorFiles) { Copy-Item (Join-Path $editorSourceDir $name) (Join-Path $editorDir $name) -Force }
    Copy-Item $sharedProfile (Join-Path $editorDir 'DariusNativeSkinProfiles.cs') -Force

    $oldRepo = $env:DARIUS_REPO_ROOT; $oldFbx = $env:DARIUS_MODEL_FBX_DIR; $oldWork = $env:DARIUS_NATIVE_WORK_ROOT
    try {
        $env:DARIUS_REPO_ROOT = $RepoRoot
        $env:DARIUS_MODEL_FBX_DIR = $fbxRoot
        $env:DARIUS_NATIVE_WORK_ROOT = $workRoot
        Invoke-Checked $UnityExe @(
            '-batchmode', '-quit', '-projectPath', $unityProject, '-buildTarget', 'StandaloneWindows64',
            '-executeMethod', 'DariusModelBundleBuilder.BuildAllBatch', '-logFile', $buildLog
        ) 'Unity native bundle build'
    } finally {
        $env:DARIUS_REPO_ROOT = $oldRepo; $env:DARIUS_MODEL_FBX_DIR = $oldFbx; $env:DARIUS_NATIVE_WORK_ROOT = $oldWork
    }

    $bundle = Join-Path $modelRoot 'darius_models.bundle'
    if (-not (Test-Path $bundle -PathType Leaf)) { throw "Unity produced no bundle: $bundle" }
    $fingerprint = Get-DariusNativeFingerprint -RepoRoot $RepoRoot -ProfilePath $sharedProfile -Profiles $models
    [System.IO.File]::WriteAllText($bundle + '.fingerprint', $fingerprint + "`n", (New-Object System.Text.UTF8Encoding($false)))
    Write-Host "Native model bundle ready: $bundle"
    Write-Host "Fingerprint: $fingerprint"
} catch {
    $preserveWorkspace = $true
    throw
} finally {
    if ($preserveWorkspace) {
        Write-Host "Keeping native asset workspace: $workRoot"
        if (Test-Path $buildLog) { Write-Host "Unity build log: $buildLog" }
    } elseif (Test-Path $workRoot) {
        Remove-Item $workRoot -Recurse -Force
    }
}
