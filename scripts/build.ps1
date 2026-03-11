param(
  [ValidateSet("Debug", "Release", "DebugCI", "ReleaseCI")]
  [string]$Configuration = "Debug",
  [ValidateSet("x64")]
  [string]$Platform = "x64"
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

$repoRoot = Split-Path -Parent $PSScriptRoot
$solution = Join-Path $repoRoot "PSVR2Toolkit.sln"
$nugetConfig = Join-Path $repoRoot ".nuget\NuGet.Config"

if (-not (Test-Path $solution)) {
  Write-Error "Solution file not found: $solution"
}
if (-not (Test-Path $nugetConfig)) {
  Write-Error "NuGet config not found: $nugetConfig"
}

$msbuildPath = & (Join-Path $PSScriptRoot "get-msbuild-path.ps1")
& $msbuildPath /restore /p:RestoreConfigFile=$nugetConfig /p:Configuration=$Configuration /p:Platform=$Platform /v:m $solution
exit $LASTEXITCODE
