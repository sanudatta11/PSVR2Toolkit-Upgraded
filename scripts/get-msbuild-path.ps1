Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

function Find-MSBuildFromVisualStudio {
  $installerRoot = Join-Path ${env:ProgramFiles(x86)} "Microsoft Visual Studio\Installer"
  $vswhere = Join-Path $installerRoot "vswhere.exe"
  if (-not (Test-Path $vswhere)) {
    return $null
  }

  $installPath = & $vswhere -latest -products * -requires Microsoft.Component.MSBuild -property installationPath
  if (-not $installPath) {
    return $null
  }

  $msbuild = Join-Path $installPath "MSBuild\Current\Bin\MSBuild.exe"
  if (Test-Path $msbuild) {
    return $msbuild
  }

  return $null
}

function Find-MSBuildFromPath {
  $cmd = Get-Command msbuild.exe -ErrorAction SilentlyContinue
  if ($cmd) {
    return $cmd.Source
  }

  return $null
}

$msbuildPath = Find-MSBuildFromPath
if (-not $msbuildPath) {
  $msbuildPath = Find-MSBuildFromVisualStudio
}

if (-not $msbuildPath) {
  Write-Error @"
MSBuild was not found.
Install Visual Studio 2022 (or Build Tools) with:
- Desktop development with C++
- .NET desktop development
Then reopen your terminal.
"@
}

Write-Output $msbuildPath
