Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

$repoRoot = Split-Path -Parent $PSScriptRoot
$solution = Join-Path $repoRoot "PSVR2Toolkit.sln"
$nugetConfig = Join-Path $repoRoot ".nuget\NuGet.Config"
$managedProjects = @(
  (Join-Path $repoRoot "projects\PSVR2Toolkit.IPC\PSVR2Toolkit.IPC.csproj"),
  (Join-Path $repoRoot "projects\PSVR2Toolkit.App\PSVR2Toolkit.App.csproj")
)

if (-not (Test-Path $solution)) {
  Write-Error "Solution file not found: $solution"
}
if (-not (Test-Path $nugetConfig)) {
  Write-Error "NuGet config not found: $nugetConfig"
}
foreach ($project in $managedProjects) {
  if (-not (Test-Path $project)) {
    Write-Error "Managed project not found: $project"
  }
}

function Invoke-DotnetManagedRestore {
  param(
    [Parameter(Mandatory = $true)]
    [bool]$UseLocalProfile
  )

  $dotnet = Get-Command dotnet.exe -ErrorAction SilentlyContinue
  if (-not $dotnet) {
    return $true
  }

  $dotnetHome = Join-Path $repoRoot ".dotnet"
  if (-not (Test-Path $dotnetHome)) {
    New-Item -ItemType Directory -Path $dotnetHome | Out-Null
  }

  $env:DOTNET_CLI_HOME = $dotnetHome
  $env:DOTNET_SKIP_FIRST_TIME_EXPERIENCE = "1"
  $env:DOTNET_ADD_GLOBAL_TOOLS_TO_PATH = "0"
  $env:DOTNET_GENERATE_ASPNET_CERTIFICATE = "0"
  $env:DOTNET_CLI_TELEMETRY_OPTOUT = "1"
  $env:NUGET_PACKAGES = Join-Path $repoRoot ".nuget\packages"
  $env:NUGET_HTTP_CACHE_PATH = Join-Path $repoRoot ".nuget\http-cache"

  if ($UseLocalProfile) {
    $localProfile = Join-Path $dotnetHome "user"
    New-Item -ItemType Directory -Force -Path (Join-Path $localProfile "AppData\Roaming") | Out-Null
    New-Item -ItemType Directory -Force -Path (Join-Path $localProfile "AppData\Local") | Out-Null
    $env:USERPROFILE = $localProfile
    $env:APPDATA = Join-Path $localProfile "AppData\Roaming"
    $env:LOCALAPPDATA = Join-Path $localProfile "AppData\Local"
  }

  foreach ($project in $managedProjects) {
    & $dotnet.Source restore $project --configfile $nugetConfig
    if ($LASTEXITCODE -ne 0) {
      return $false
    }
  }

  return $true
}

if (-not (Invoke-DotnetManagedRestore -UseLocalProfile:$false)) {
  if (-not (Invoke-DotnetManagedRestore -UseLocalProfile:$true)) {
    exit 1
  }
}

$nuget = Get-Command nuget.exe -ErrorAction SilentlyContinue
if ($nuget) {
  & $nuget.Source restore $solution -ConfigFile $nugetConfig
  exit $LASTEXITCODE
}

# No additional restore step is required when nuget.exe is unavailable.
exit 0
