Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

<#
.SYNOPSIS
    Fully installs PSVR2 Toolkit including driver and SteamVR settings integration.

.DESCRIPTION
    This script automates the complete installation:
    1. Installs the PSVR2 Toolkit driver DLL
    2. Installs SteamVR settings integration (optional)
    3. Verifies all installations

.PARAMETER SourceDll
    Path to the PSVR2 Toolkit driver DLL (driver_playstation_vr2.dll)

.PARAMETER SteamPath
    Optional: Override Steam installation path if auto-detection fails

.PARAMETER SkipSteamVRSettings
    Skip installing the SteamVR settings integration

.PARAMETER Force
    Overwrite existing backup if present

.EXAMPLE
    .\driver-install-full.ps1 -SourceDll .\driver_playstation_vr2.dll

.EXAMPLE
    .\driver-install-full.ps1 -SourceDll .\driver_playstation_vr2.dll -SkipSteamVRSettings
#>

param(
    [Parameter(Mandatory=$true)]
    [string]$SourceDll,

    [Parameter(Mandatory=$false)]
    [string]$SteamPath,

    [Parameter(Mandatory=$false)]
    [switch]$SkipSteamVRSettings,

    [Parameter(Mandatory=$false)]
    [switch]$Force
)

function Write-Log {
    param([string]$Message, [string]$Level = "INFO")
    $timestamp = Get-Date -Format "yyyy-MM-dd HH:mm:ss"
    $logMessage = "[$timestamp] [$Level] $Message"
    Write-Host $logMessage
    return $logMessage
}

function Find-SteamPath {
    if ($SteamPath) {
        if (Test-Path $SteamPath) {
            return $SteamPath
        }
        Write-Log "Provided Steam path does not exist: $SteamPath" "ERROR"
        return $null
    }

    try {
        $regPath = "HKLM:\SOFTWARE\Valve\Steam"
        if (Test-Path $regPath) {
            $installPath = (Get-ItemProperty -Path $regPath -Name "InstallPath" -ErrorAction SilentlyContinue).InstallPath
            if ($installPath -and (Test-Path $installPath)) {
                Write-Log "Found Steam via registry: $installPath"
                return $installPath
            }
        }
    } catch {
        Write-Log "Registry lookup failed: $_" "WARN"
    }

    $commonPaths = @(
        "C:\Program Files (x86)\Steam",
        "C:\Program Files\Steam",
        "D:\Steam",
        "E:\Steam"
    )

    foreach ($path in $commonPaths) {
        if (Test-Path $path) {
            Write-Log "Found Steam at common path: $path"
            return $path
        }
    }

    return $null
}

function Find-DriverPath {
    param([string]$SteamRoot)

    $relativePaths = @(
        "steamapps\common\PS VR2\SteamVR_Plug-In\bin\win64",
        "SteamApps\common\PS VR2\SteamVR_Plug-In\bin\win64"
    )

    foreach ($relativePath in $relativePaths) {
        $fullPath = Join-Path $SteamRoot $relativePath
        if (Test-Path $fullPath) {
            return $fullPath
        }
    }

    return $null
}

function Install-Driver {
    param([string]$SourceDllPath, [string]$DriverPath, [bool]$ForceInstall)

    Write-Log "=== Driver Installation ===" "INFO"

    $originalDll = Join-Path $DriverPath "driver_playstation_vr2.dll"
    $backupDll = Join-Path $DriverPath "driver_playstation_vr2_orig.dll"

    if (-not (Test-Path $originalDll)) {
        Write-Log "Original Sony driver DLL not found: $originalDll" "ERROR"
        return $false
    }

    if (Test-Path $backupDll) {
        if ($ForceInstall) {
            Write-Log "Backup already exists. Force flag set, will overwrite." "WARN"
        } else {
            Write-Log "Backup already exists: $backupDll" "ERROR"
            Write-Log "Driver may already be installed. Use -Force to overwrite." "ERROR"
            return $false
        }
    }

    try {
        Write-Log "Step 1: Backing up original Sony driver..."
        Copy-Item -Path $originalDll -Destination $backupDll -Force
        Write-Log "  [OK] Backup created"

        Write-Log "Step 2: Removing original driver..."
        Remove-Item -Path $originalDll -Force
        Write-Log "  [OK] Original driver removed"

        Write-Log "Step 3: Installing PSVR2 Toolkit driver..."
        Copy-Item -Path $SourceDllPath -Destination $originalDll -Force
        Write-Log "  [OK] Toolkit driver installed"

        if ((Test-Path $originalDll) -and (Test-Path $backupDll)) {
            Write-Log "  [OK] Driver installation verified"
            return $true
        }

        Write-Log "Driver verification failed!" "ERROR"
        return $false

    } catch {
        Write-Log "Driver installation failed: $_" "ERROR"
        if (Test-Path $backupDll) {
            Write-Log "Attempting to restore backup..." "WARN"
            Copy-Item -Path $backupDll -Destination $originalDll -Force
            Remove-Item -Path $backupDll -Force
        }
        return $false
    }
}

function Install-SteamVRSettings {
    param([string]$SteamRoot)

    Write-Log ""
    Write-Log "=== SteamVR Settings Integration ===" "INFO"

    $scriptDir = Split-Path -Parent $PSCommandPath
    $resourcesDir = Join-Path (Split-Path -Parent $scriptDir) "resources\steamvr-settings"

    if (-not (Test-Path $resourcesDir)) {
        Write-Log "SteamVR settings resources not found: $resourcesDir" "WARN"
        return $false
    }

    try {
        # Install resource driver for localization
        $steamVRDriversPath = Join-Path $SteamRoot "steamapps\common\SteamVR\drivers"
        $targetDriverPath = Join-Path $steamVRDriversPath "playstation_vr2_ex"

        Write-Log "Step 1: Installing localization resources..."
        $sourceDriverPath = Join-Path $resourcesDir "playstation_vr2_ex"

        if (Test-Path $sourceDriverPath) {
            if (Test-Path $targetDriverPath) {
                Remove-Item -Path $targetDriverPath -Recurse -Force
            }
            Copy-Item -Path $sourceDriverPath -Destination $targetDriverPath -Recurse -Force
            Write-Log "  [OK] Localization resources installed"
        } else {
            Write-Log "  [FAIL] Source driver path not found: $sourceDriverPath" "WARN"
            return $false
        }

        # Install settings schema
        Write-Log "Step 2: Installing settings schema..."
        $psvr2ResourcesPath = Join-Path $SteamRoot "steamapps\common\PS VR2\SteamVR_Plug-In\resources"

        # Try alternative casing
        if (-not (Test-Path $psvr2ResourcesPath)) {
            $psvr2ResourcesPath = Join-Path $SteamRoot "SteamApps\common\PS VR2\SteamVR_Plug-In\resources"
        }

        if (-not (Test-Path $psvr2ResourcesPath)) {
            Write-Log "  [FAIL] PS VR2 resources directory not found" "WARN"
            return $false
        }

        $settingsDir = Join-Path $psvr2ResourcesPath "settings"
        if (-not (Test-Path $settingsDir)) {
            New-Item -ItemType Directory -Path $settingsDir -Force | Out-Null
            Write-Log "  [OK] Created settings directory"
        }

        $sourceSchema = Join-Path $resourcesDir "settingsschema.vrsettings"
        $targetSchema = Join-Path $settingsDir "settingsschema.vrsettings"

        if (Test-Path $sourceSchema) {
            Copy-Item -Path $sourceSchema -Destination $targetSchema -Force
            Write-Log "  [OK] Settings schema installed"
        } else {
            Write-Log "  [FAIL] Source schema not found: $sourceSchema" "WARN"
            return $false
        }

        Write-Log "  [OK] SteamVR settings integration complete"
        return $true

    } catch {
        Write-Log "SteamVR settings installation failed: $_" "WARN"
        return $false
    }
}

# Main installation logic
try {
    Write-Log "=== PSVR2 Toolkit Full Installation ===" "INFO"
    Write-Log ""

    # Validate source DLL
    if (-not (Test-Path $SourceDll)) {
        Write-Log "Source DLL not found: $SourceDll" "ERROR"
        exit 1
    }

    # Security: Validate DLL file extension
    if (-not $SourceDll.EndsWith(".dll")) {
        Write-Log "Source file must be a .dll file" "ERROR"
        exit 1
    }

    $sourceDllFullPath = (Resolve-Path $SourceDll).Path
    Write-Log "Source DLL: $sourceDllFullPath"

    # Security: Validate file size
    $fileInfo = Get-Item $sourceDllFullPath
    if ($fileInfo.Length -eq 0) {
        Write-Log "DLL file is empty (0 bytes)" "ERROR"
        exit 1
    }
    if ($fileInfo.Length -gt 10MB) {
        Write-Log "DLL file is suspiciously large: $($fileInfo.Length) bytes (>10MB)" "ERROR"
        exit 1
    }
    Write-Log "DLL file size: $($fileInfo.Length) bytes"

    # Security: Check digital signature (warning only)
    try {
        $signature = Get-AuthenticodeSignature $sourceDllFullPath
        if ($signature.Status -eq "Valid") {
            Write-Log "DLL is digitally signed by: $($signature.SignerCertificate.Subject)" "INFO"
        } else {
            Write-Log "Warning: DLL is not digitally signed (Status: $($signature.Status))" "WARN"
            Write-Log "This is expected for development builds but be cautious with untrusted sources" "WARN"
        }
    } catch {
        Write-Log "Could not verify digital signature: $_" "WARN"
    }

    # Find Steam installation
    Write-Log "Locating Steam installation..."
    $steamPath = Find-SteamPath
    if (-not $steamPath) {
        Write-Log "Could not locate Steam installation. Please use -SteamPath parameter." "ERROR"
        exit 1
    }

    # Find driver directory
    Write-Log "Locating PS VR2 driver directory..."
    $driverPath = Find-DriverPath -SteamRoot $steamPath
    if (-not $driverPath) {
        Write-Log "Could not locate PS VR2 driver directory." "ERROR"
        exit 1
    }

    Write-Log "Driver directory: $driverPath"
    Write-Log ""

    # Install driver
    $driverSuccess = Install-Driver -SourceDllPath $sourceDllFullPath -DriverPath $driverPath -ForceInstall $Force

    if (-not $driverSuccess) {
        Write-Log "Driver installation failed. Aborting." "ERROR"
        exit 1
    }

    # Install SteamVR settings integration
    $settingsSuccess = $false
    if (-not $SkipSteamVRSettings) {
        $settingsSuccess = Install-SteamVRSettings -SteamRoot $steamPath
    } else {
        Write-Log ""
        Write-Log "Skipping SteamVR settings integration (--SkipSteamVRSettings flag set)" "INFO"
    }

    # Write log file
    Write-Log ""
    Write-Log "=== Writing Installation Log ===" "INFO"
    $logFile = Join-Path $driverPath "install_log.txt"
    $logContent = @"
PSVR2 Toolkit Full Installation Log
====================================
Timestamp: $(Get-Date -Format "yyyy-MM-dd HH:mm:ss")
Steam Path: $steamPath
Driver Path: $driverPath
Source DLL: $sourceDllFullPath

Installation Results:
- Driver: SUCCESS
- SteamVR Settings: $(if ($settingsSuccess) { "SUCCESS" } elseif ($SkipSteamVRSettings) { "SKIPPED" } else { "FAILED (non-critical)" })

Files Installed:
- driver_playstation_vr2.dll (PSVR2 Toolkit)
- driver_playstation_vr2_orig.dll (Sony backup)
$(if ($settingsSuccess) { "- SteamVR settings integration (in-VR settings menu)" })

To restore the original driver, run: .\driver-restore.ps1
"@

    try {
        $logContent | Out-File -FilePath $logFile -Encoding UTF8 -Append
        Write-Log "  [OK] Log written to: $logFile"
    } catch {
        Write-Log "Failed to write log file: $_" "WARN"
    }

    Write-Log ""
    Write-Log "=== Installation Complete ===" "INFO"
    Write-Log "PSVR2 Toolkit has been successfully installed."
    if ($settingsSuccess) {
        Write-Log "SteamVR settings integration enabled - access via SteamVR Settings > PlayStation VR2"
    }
    Write-Log "Please restart SteamVR for changes to take effect."
    Write-Log ""

    exit 0

} catch {
    Write-Log "Unexpected error during installation: $_" "ERROR"
    Write-Log "$($_.ScriptStackTrace)" "ERROR"
    exit 1
}
