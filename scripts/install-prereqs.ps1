Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

if (-not (Get-Command winget -ErrorAction SilentlyContinue)) {
  Write-Error "winget is not available. Install App Installer from Microsoft Store first."
}

function Invoke-WingetInstall {
  param(
    [Parameter(Mandatory = $true)]
    [string]$Id,
    [Parameter(Mandatory = $false)]
    [string]$OverrideArgs
  )

  $args = @("install", "--id", $Id, "--exact", "--accept-source-agreements", "--accept-package-agreements")
  if ($OverrideArgs) {
    $args += @("--override", $OverrideArgs)
  }

  & winget @args
  if ($LASTEXITCODE -ne 0) {
    throw "winget install failed for '$Id' with exit code $LASTEXITCODE"
  }
}

$vsOverride = "--wait --passive --add Microsoft.VisualStudio.Workload.VCTools --add Microsoft.VisualStudio.Workload.ManagedDesktopBuildTools --includeRecommended"
Write-Host "Installing Visual Studio Build Tools (prefer 2026 channel)..."
try {
  Invoke-WingetInstall -Id "Microsoft.VisualStudio.BuildTools" -OverrideArgs $vsOverride
} catch {
  Write-Warning "BuildTools 2026 install did not complete: $($_.Exception.Message)"
  Write-Host "Falling back to Visual Studio Build Tools 2022..."
  Invoke-WingetInstall -Id "Microsoft.VisualStudio.2022.BuildTools" -OverrideArgs $vsOverride
}

Write-Host "Installing NuGet command-line..."
Invoke-WingetInstall -Id "Microsoft.NuGet"

Write-Host "Done. Restart your terminal, then run:"
Write-Host "  powershell -NoProfile -ExecutionPolicy Bypass -File .\scripts\restore.ps1"
Write-Host "  powershell -NoProfile -ExecutionPolicy Bypass -File .\scripts\build.ps1 -Configuration Debug"
