param(
    [Parameter(Mandatory = $true)][string]$RepoRoot
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

$RepoRoot = (Resolve-Path $RepoRoot).Path
$contract = Join-Path $PSScriptRoot 'Native-Model-Contract.ps1'
$profile = Join-Path $RepoRoot 'src\DariusPrototype\Native\DariusNativeSkinProfiles.cs'
. $contract
$fingerprint = Test-DariusNativeBundleFingerprint -RepoRoot $RepoRoot -ProfilePath $profile
Write-Host "Native model bundle fingerprint verified: $fingerprint"
