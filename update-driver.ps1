# PSVR2 Toolkit Driver Update Script
# Automatically builds and updates the PSVR2 driver DLL

param(
    [Parameter(Mandatory=$false)]
    [string]$Configuration = "Release",

    [Parameter(Mandatory=$false)]
    [switch]$SkipBuild
)

Write-Host "========================================" -ForegroundColor Cyan
Write-Host "PSVR2 Toolkit Driver Update Script" -ForegroundColor Cyan
Write-Host "========================================" -ForegroundColor Cyan
Write-Host ""

# Check if running as Administrator
$isAdmin = ([Security.Principal.WindowsPrincipal] [Security.Principal.WindowsIdentity]::GetCurrent()).IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)
if (-not $isAdmin) {
    Write-Host "ERROR: This script requires Administrator privileges." -ForegroundColor Red
    Write-Host "Please right-click PowerShell and select 'Run as Administrator'" -ForegroundColor Yellow
    Write-Host ""
    pause
    exit 1
}

# Get script directory
$scriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path

# Build the driver if not skipped
if (-not $SkipBuild) {
    Write-Host "[1/5] Building PSVR2 Toolkit driver..." -ForegroundColor Yellow

    $solutionPath = Join-Path $scriptDir "PSVR2Toolkit.sln"
    if (-not (Test-Path $solutionPath)) {
        Write-Host "ERROR: Solution file not found: $solutionPath" -ForegroundColor Red
        Write-Host ""
        pause
        exit 1
    }

    # Restore NuGet packages
    Write-Host "  Restoring NuGet packages..." -ForegroundColor Cyan
    & nuget restore $solutionPath 2>&1 | Out-Null

    # Build the driver project
    Write-Host "  Building driver (Configuration: $Configuration)..." -ForegroundColor Cyan
    $buildOutput = & msbuild /m /p:Configuration=$Configuration /p:Platform=x64 /t:psvr2_openvr_driver_ex:Rebuild $solutionPath 2>&1

    if ($LASTEXITCODE -ne 0) {
        Write-Host "ERROR: Build failed!" -ForegroundColor Red
        Write-Host $buildOutput
        Write-Host ""
        pause
        exit 1
    }

    Write-Host "  Build completed successfully!" -ForegroundColor Green
    Write-Host ""
} else {
    Write-Host "[1/5] Skipping build (using existing DLL)..." -ForegroundColor Yellow
    Write-Host ""
}

# Find the built DLL
$sourceDll = Join-Path $scriptDir "x64\$Configuration\driver_playstation_vr2.dll"
if (-not (Test-Path $sourceDll)) {
    Write-Host "ERROR: Built DLL not found: $sourceDll" -ForegroundColor Red
    Write-Host "Please ensure the build completed successfully" -ForegroundColor Yellow
    Write-Host ""
    pause
    exit 1
}

Write-Host "  Using DLL: $sourceDll" -ForegroundColor Green
Write-Host ""

Write-Host "[2/5] Finding Steam installation..." -ForegroundColor Yellow

# Find Steam installation
function Find-SteamPath {
    # Try registry first
    try {
        $regPath = "HKLM:\SOFTWARE\WOW6432Node\Valve\Steam"
        if (Test-Path $regPath) {
            $steamPath = (Get-ItemProperty -Path $regPath -Name "InstallPath" -ErrorAction SilentlyContinue).InstallPath
            if ($steamPath -and (Test-Path $steamPath)) {
                return $steamPath
            }
        }
    } catch {}

    # Try common paths
    $commonPaths = @(
        "C:\Program Files (x86)\Steam",
        "C:\Program Files\Steam",
        "D:\Steam",
        "E:\Steam"
    )

    foreach ($path in $commonPaths) {
        if (Test-Path $path) {
            return $path
        }
    }

    return $null
}

# Get Steam library folders
function Get-SteamLibraryFolders {
    param([string]$SteamPath)

    $libraries = @()

    # Add main Steam path
    $libraries += $SteamPath

    # Parse libraryfolders.vdf
    $vdfPath = Join-Path $SteamPath "steamapps\libraryfolders.vdf"
    if (-not (Test-Path $vdfPath)) {
        $vdfPath = Join-Path $SteamPath "SteamApps\libraryfolders.vdf"
    }

    if (Test-Path $vdfPath) {
        $content = Get-Content $vdfPath
        foreach ($line in $content) {
            if ($line -match '"path"\s+"(.+)"') {
                $libPath = $matches[1] -replace '\\\\', '\'
                if (Test-Path $libPath) {
                    $libraries += $libPath
                }
            }
        }
    }

    return $libraries
}

# Find PS VR2 driver directory
function Find-DriverPath {
    param([string]$SteamPath)

    $relativePaths = @(
        "steamapps\common\PlayStation VR2 App\SteamVR_Plug-In\bin\win64",
        "SteamApps\common\PlayStation VR2 App\SteamVR_Plug-In\bin\win64",
        "steamapps\common\PS VR2\SteamVR_Plug-In\bin\win64",
        "SteamApps\common\PS VR2\SteamVR_Plug-In\bin\win64"
    )

    $libraries = Get-SteamLibraryFolders -SteamPath $SteamPath

    foreach ($library in $libraries) {
        foreach ($relPath in $relativePaths) {
            $fullPath = Join-Path $library $relPath
            if (Test-Path $fullPath) {
                return $fullPath
            }
        }
    }

    return $null
}

$steamPath = Find-SteamPath
if (-not $steamPath) {
    Write-Host "ERROR: Could not find Steam installation" -ForegroundColor Red
    Write-Host "Please install Steam first" -ForegroundColor Yellow
    Write-Host ""
    pause
    exit 1
}

Write-Host "  Found Steam at: $steamPath" -ForegroundColor Green
Write-Host ""

Write-Host "[3/5] Finding PS VR2 driver directory..." -ForegroundColor Yellow

# Get and display all Steam library folders
$libraries = Get-SteamLibraryFolders -SteamPath $steamPath
Write-Host "  Checking Steam libraries:" -ForegroundColor Cyan
foreach ($lib in $libraries) {
    Write-Host "    - $lib" -ForegroundColor Gray
}
Write-Host ""

$driverPath = Find-DriverPath -SteamPath $steamPath
if (-not $driverPath) {
    Write-Host "ERROR: Could not find PS VR2 driver directory" -ForegroundColor Red
    Write-Host "Please install PS VR2 app from Steam first" -ForegroundColor Yellow
    Write-Host ""
    Write-Host "Searched in the following locations:" -ForegroundColor Yellow
    foreach ($lib in $libraries) {
        Write-Host "  $lib\steamapps\common\PlayStation VR2 App\SteamVR_Plug-In\bin\win64" -ForegroundColor Gray
    }
    Write-Host ""
    pause
    exit 1
}

Write-Host "  Found driver at: $driverPath" -ForegroundColor Green
Write-Host ""

Write-Host "[4/5] Installing PSVR2 Toolkit driver..." -ForegroundColor Yellow

$targetDll = Join-Path $driverPath "driver_playstation_vr2.dll"
$origDll = Join-Path $driverPath "driver_playstation_vr2_orig.dll"

# If _orig doesn't exist, rename current DLL to _orig (first-time setup)
if (-not (Test-Path $origDll)) {
    if (Test-Path $targetDll) {
        Write-Host "  First-time setup: Renaming Sony driver to driver_playstation_vr2_orig.dll..." -ForegroundColor Cyan
        try {
            Rename-Item -Path $targetDll -NewName "driver_playstation_vr2_orig.dll" -Force
            Write-Host "  Original Sony driver preserved as _orig.dll" -ForegroundColor Green
        } catch {
            Write-Host "ERROR: Failed to rename original driver: $_" -ForegroundColor Red
            Write-Host ""
            pause
            exit 1
        }
    } else {
        Write-Host "ERROR: No existing driver found at $targetDll" -ForegroundColor Red
        Write-Host "Please ensure PS VR2 app is properly installed" -ForegroundColor Yellow
        Write-Host ""
        pause
        exit 1
    }
} else {
    Write-Host "  Original Sony driver already preserved (_orig.dll exists)" -ForegroundColor Green
}

# Install/update toolkit wrapper driver
Write-Host "  Installing PSVR2 Toolkit wrapper driver..." -ForegroundColor Cyan
try {
    Copy-Item -Path $sourceDll -Destination $targetDll -Force
    Write-Host "  Toolkit driver installed successfully!" -ForegroundColor Green
} catch {
    Write-Host "ERROR: Failed to install driver: $_" -ForegroundColor Red
    Write-Host ""
    pause
    exit 1
}

Write-Host ""
Write-Host "========================================" -ForegroundColor Green
Write-Host "Installation Complete!" -ForegroundColor Green
Write-Host "========================================" -ForegroundColor Green
Write-Host ""
Write-Host "Driver files in: $driverPath" -ForegroundColor Cyan
Write-Host "  - driver_playstation_vr2.dll (PSVR2 Toolkit wrapper)" -ForegroundColor Cyan
Write-Host "  - driver_playstation_vr2_orig.dll (Original Sony driver)" -ForegroundColor Cyan
Write-Host ""
Write-Host "Next steps:" -ForegroundColor Yellow
Write-Host "  1. Restart SteamVR" -ForegroundColor White
Write-Host "  2. Run PSVR2Toolkit.App.exe for calibration and settings" -ForegroundColor White
Write-Host ""
pause
