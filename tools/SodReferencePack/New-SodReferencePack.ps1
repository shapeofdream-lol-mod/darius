param(
    [string]$GameDir = $env:SOD_GAME_DIR,
    [string]$Version = '',
    [string]$OutputDir = ''
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

if ([string]::IsNullOrWhiteSpace($GameDir)) {
    throw 'Set -GameDir or SOD_GAME_DIR to the Shape of Dreams install directory.'
}

$repoRoot = [System.IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..\..'))
$managedDir = Join-Path $GameDir 'Shape of Dreams_Data\Managed'
$projectPath = Join-Path $repoRoot 'src\DariusPrototype\DariusPrototype.csproj'
$packProject = Join-Path $PSScriptRoot 'SodReferencePack.csproj'
if ([string]::IsNullOrWhiteSpace($OutputDir)) {
    $OutputDir = Join-Path $repoRoot 'build\reference-pack'
}

if (-not (Test-Path $managedDir)) {
    throw "Managed assemblies directory not found: $managedDir"
}

[xml]$project = Get-Content $projectPath -Raw
if ([string]::IsNullOrWhiteSpace($Version)) {
    $Version = @(
        $project.Project.PropertyGroup |
            ForEach-Object { [string]$_.SodReferencePackVersion } |
            Where-Object { -not [string]::IsNullOrWhiteSpace($_) }
    ) | Select-Object -First 1
}
if ([string]::IsNullOrWhiteSpace($Version)) {
    throw 'SodReferencePackVersion is missing from DariusPrototype.csproj.'
}

$referenceNames = @(
    $project.Project.ItemGroup.Reference |
        ForEach-Object { [string]$_.Include } |
        Where-Object { -not [string]::IsNullOrWhiteSpace($_) } |
        Sort-Object -Unique
)
if ($referenceNames.Count -eq 0) {
    throw 'No assembly references were found in DariusPrototype.csproj.'
}

$assemblies = @()
foreach ($name in $referenceNames) {
    $path = Join-Path $managedDir ($name + '.dll')
    if (-not (Test-Path $path)) {
        throw "Required game assembly not found: $path"
    }
    $assemblies += $path
}

$tempRoot = Join-Path ([System.IO.Path]::GetTempPath()) ('sod-reference-pack-' + [Guid]::NewGuid().ToString('N'))
$toolDir = Join-Path $tempRoot 'tool'
$referenceDir = Join-Path $tempRoot 'ref'

try {
    New-Item -ItemType Directory -Force -Path $toolDir, $referenceDir, $OutputDir | Out-Null

    & dotnet tool install JetBrains.Refasmer.CliTool --tool-path $toolDir --version 2.0.3 --verbosity quiet
    if ($LASTEXITCODE -ne 0) { throw 'Failed to install JetBrains.Refasmer.CliTool.' }

    $refasmer = Join-Path $toolDir 'refasmer.exe'
    if (-not (Test-Path $refasmer)) { $refasmer = Join-Path $toolDir 'refasmer' }
    if (-not (Test-Path $refasmer)) { throw 'Refasmer executable was not installed.' }

    & $refasmer -q --public --omit-non-api-members=true -O $referenceDir @assemblies
    if ($LASTEXITCODE -ne 0) { throw 'Refasmer failed to generate reference assemblies.' }

    & dotnet pack $packProject -c Release -o $OutputDir "-p:ReferencePackDir=$referenceDir" "-p:PackageVersion=$Version" --nologo
    if ($LASTEXITCODE -ne 0) { throw 'Failed to create the reference package.' }

    $package = Get-ChildItem $OutputDir -Filter "ShapeOfDreams.ReferenceAssemblies.$Version.nupkg" | Select-Object -First 1
    if ($null -eq $package) { throw 'Reference package was not created.' }

    Write-Host "Reference package ready: $($package.FullName)"
}
finally {
    if (Test-Path $tempRoot) { Remove-Item $tempRoot -Recurse -Force }
}
