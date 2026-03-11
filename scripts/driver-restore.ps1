Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

<#
.SYNOPSIS
    Restores the original Sony PS VR2 driver by removing the PSVR2 Toolkit.

.DESCRIPTION
    This script reverses the installation:
    1. Locates the Steam PS VR2 installation
    2. Removes the PSVR2 Toolkit driver DLL
    3. Restores the original Sony driver DLL from backup
    4. Verifies the restoration

.PARAMETER SteamPath
    Optional: Override Steam installation path if auto-detection fails

.PARAMETER KeepBackup
    Keep the backup DLL file after restoration (renamed to .bak)

.EXAMPLE
    .\driver-restore.ps1

.EXAMPLE
    .\driver-restore.ps1 -KeepBackup
#>

param(
    [Parameter(Mandatory=$false)]
    [string]$SteamPath,
    
    [Parameter(Mandatory=$false)]
    [switch]$KeepBackup
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

    # Try registry first
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

    # Try common installation paths
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
    
    $relativePath = "steamapps\common\PS VR2\SteamVR_Plug-In\bin\win64"
    $driverPath = Join-Path $SteamRoot $relativePath
    
    if (Test-Path $driverPath) {
        return $driverPath
    }
    
    # Try alternative casing
    $relativePath = "SteamApps\common\PS VR2\SteamVR_Plug-In\bin\win64"
    $driverPath = Join-Path $SteamRoot $relativePath
    
    if (Test-Path $driverPath) {
        return $driverPath
    }
    
    return $null
}

# Main restoration logic
try {
    Write-Log "=== PSVR2 Toolkit Driver Restoration ===" "INFO"
    Write-Log ""
    
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
        Write-Log "Could not locate PS VR2 driver directory in Steam installation." "ERROR"
        Write-Log "Expected path: steamapps\common\PS VR2\SteamVR_Plug-In\bin\win64" "ERROR"
        exit 1
    }
    
    Write-Log "Driver directory: $driverPath"
    
    # Define file paths
    $currentDll = Join-Path $driverPath "driver_playstation_vr2.dll"
    $backupDll = Join-Path $driverPath "driver_playstation_vr2_orig.dll"
    $backupBak = Join-Path $driverPath "driver_playstation_vr2_orig.dll.bak"
    $logFile = Join-Path $driverPath "install_log.txt"
    
    # Check if backup exists
    if (-not (Test-Path $backupDll)) {
        Write-Log "Backup DLL not found: $backupDll" "ERROR"
        Write-Log "Cannot restore without backup. The toolkit may not be installed." "ERROR"
        exit 1
    }
    
    # Check if current DLL exists
    if (-not (Test-Path $currentDll)) {
        Write-Log "Current driver DLL not found: $currentDll" "ERROR"
        Write-Log "This is unexpected. Manual intervention may be required." "ERROR"
        exit 1
    }
    
    Write-Log ""
    Write-Log "=== Restoration Steps ===" "INFO"
    
    # Step 1: Remove toolkit DLL
    Write-Log "Step 1: Removing PSVR2 Toolkit driver..."
    try {
        Remove-Item -Path $currentDll -Force
        Write-Log "  ✓ Toolkit driver removed"
    } catch {
        Write-Log "Failed to remove toolkit driver: $_" "ERROR"
        exit 1
    }
    
    # Step 2: Restore original DLL
    Write-Log "Step 2: Restoring original Sony driver..."
    try {
        Copy-Item -Path $backupDll -Destination $currentDll -Force
        Write-Log "  ✓ Original driver restored"
    } catch {
        Write-Log "Failed to restore original driver: $_" "ERROR"
        Write-Log "CRITICAL: driver_playstation_vr2.dll is missing!" "ERROR"
        Write-Log "Manually copy driver_playstation_vr2_orig.dll to driver_playstation_vr2.dll" "ERROR"
        exit 1
    }
    
    # Step 3: Handle backup file
    if ($KeepBackup) {
        Write-Log "Step 3: Renaming backup to .bak..."
        try {
            Move-Item -Path $backupDll -Destination $backupBak -Force
            Write-Log "  ✓ Backup renamed to: driver_playstation_vr2_orig.dll.bak"
        } catch {
            Write-Log "Failed to rename backup: $_" "WARN"
        }
    } else {
        Write-Log "Step 3: Removing backup file..."
        try {
            Remove-Item -Path $backupDll -Force
            Write-Log "  ✓ Backup file removed"
        } catch {
            Write-Log "Failed to remove backup: $_" "WARN"
        }
    }
    
    # Step 4: Verify restoration
    Write-Log "Step 4: Verifying restoration..."
    $verifyOriginal = Test-Path $currentDll
    $verifyBackupGone = -not (Test-Path $backupDll)
    
    if ($verifyOriginal -and ($verifyBackupGone -or $KeepBackup)) {
        Write-Log "  ✓ Verification passed"
        Write-Log "    - driver_playstation_vr2.dll (Sony original): $(Test-Path $currentDll)"
        if ($KeepBackup) {
            Write-Log "    - driver_playstation_vr2_orig.dll.bak (backup): $(Test-Path $backupBak)"
        }
    } else {
        Write-Log "Verification failed!" "ERROR"
        Write-Log "  - driver_playstation_vr2.dll: $verifyOriginal" "ERROR"
        Write-Log "  - Backup removed: $verifyBackupGone" "ERROR"
        exit 1
    }
    
    # Step 5: Write log entry
    Write-Log "Step 5: Writing restoration log..."
    $logContent = @"

==============================
PSVR2 Toolkit Restoration Log
==============================
Timestamp: $(Get-Date -Format "yyyy-MM-dd HH:mm:ss")
Steam Path: $steamPath
Driver Path: $driverPath
Status: SUCCESS

Original Sony driver has been restored.
PSVR2 Toolkit has been removed.

"@
    
    try {
        $logContent | Out-File -FilePath $logFile -Encoding UTF8 -Append
        Write-Log "  ✓ Log written to: $logFile"
    } catch {
        Write-Log "Failed to write log file: $_" "WARN"
    }
    
    Write-Log ""
    Write-Log "=== Restoration Complete ===" "INFO"
    Write-Log "Original Sony driver has been successfully restored."
    Write-Log "PSVR2 Toolkit has been removed."
    Write-Log "Please restart SteamVR for changes to take effect."
    Write-Log ""
    
    exit 0
    
} catch {
    Write-Log "Unexpected error during restoration: $_" "ERROR"
    Write-Log $_.ScriptStackTrace "ERROR"
    exit 1
}
