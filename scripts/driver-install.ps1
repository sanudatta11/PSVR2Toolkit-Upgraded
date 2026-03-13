Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

<#
.SYNOPSIS
    Safely installs the PSVR2 Toolkit driver by backing up and replacing the Sony driver DLL.

.DESCRIPTION
    This script automates the installation process:
    1. Locates the Steam PS VR2 installation
    2. Backs up the original Sony driver DLL
    3. Installs the PSVR2 Toolkit driver DLL
    4. Verifies the installation

.PARAMETER SourceDll
    Path to the PSVR2 Toolkit driver DLL (driver_playstation_vr2.dll)

.PARAMETER SteamPath
    Optional: Override Steam installation path if auto-detection fails

.PARAMETER Force
    Overwrite existing backup if present

.EXAMPLE
    .\driver-install.ps1 -SourceDll .\driver_playstation_vr2.dll

.EXAMPLE
    .\driver-install.ps1 -SourceDll .\driver_playstation_vr2.dll -Force
#>

param(
    [Parameter(Mandatory=$true)]
    [string]$SourceDll,

    [Parameter(Mandatory=$false)]
    [string]$SteamPath,

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

# Main installation logic
try {
    Write-Log "=== PSVR2 Toolkit Driver Installation ===" "INFO"
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
        Write-Log "Could not locate PS VR2 driver directory in Steam installation." "ERROR"
        Write-Log "Expected path: steamapps\common\PS VR2\SteamVR_Plug-In\bin\win64" "ERROR"
        exit 1
    }

    Write-Log "Driver directory: $driverPath"

    # Define file paths
    $originalDll = Join-Path $driverPath "driver_playstation_vr2.dll"
    $backupDll = Join-Path $driverPath "driver_playstation_vr2_orig.dll"
    $logFile = Join-Path $driverPath "install_log.txt"

    # Check if original DLL exists
    if (-not (Test-Path $originalDll)) {
        Write-Log "Original Sony driver DLL not found: $originalDll" "ERROR"
        Write-Log "Please ensure PS VR2 app is installed via Steam." "ERROR"
        exit 1
    }

    # Check if backup already exists
    if (Test-Path $backupDll) {
        if ($Force) {
            Write-Log "Backup already exists. Force flag set, will overwrite." "WARN"
        } else {
            Write-Log "Backup already exists: $backupDll" "ERROR"
            Write-Log "This suggests the toolkit may already be installed." "ERROR"
            Write-Log "Use -Force to overwrite the existing backup (not recommended)." "ERROR"
            exit 1
        }
    }

    Write-Log ""
    Write-Log "=== Installation Steps ===" "INFO"

    # Step 1: Backup original DLL
    Write-Log "Step 1: Backing up original Sony driver..."
    try {
        Copy-Item -Path $originalDll -Destination $backupDll -Force
        Write-Log "  ✓ Backup created: driver_playstation_vr2_orig.dll"
    } catch {
        Write-Log "Failed to create backup: $_" "ERROR"
        exit 1
    }

    # Step 2: Remove original DLL
    Write-Log "Step 2: Removing original driver..."
    try {
        Remove-Item -Path $originalDll -Force
        Write-Log "  ✓ Original driver removed"
    } catch {
        Write-Log "Failed to remove original driver: $_" "ERROR"
        Write-Log "Attempting to restore backup..." "WARN"
        Copy-Item -Path $backupDll -Destination $originalDll -Force
        Remove-Item -Path $backupDll -Force
        exit 1
    }

    # Step 3: Copy toolkit DLL
    Write-Log "Step 3: Installing PSVR2 Toolkit driver..."
    try {
        Copy-Item -Path $sourceDllFullPath -Destination $originalDll -Force
        Write-Log "  ✓ Toolkit driver installed"
    } catch {
        Write-Log "Failed to install toolkit driver: $_" "ERROR"
        Write-Log "Attempting to restore backup..." "WARN"
        Copy-Item -Path $backupDll -Destination $originalDll -Force
        Remove-Item -Path $backupDll -Force
        exit 1
    }

    # Step 4: Verify installation
    Write-Log "Step 4: Verifying installation..."
    $verifyOriginal = Test-Path $originalDll
    $verifyBackup = Test-Path $backupDll

    if ($verifyOriginal -and $verifyBackup) {
        Write-Log "  ✓ Verification passed"
        Write-Log "    - driver_playstation_vr2.dll (toolkit): $(Test-Path $originalDll)"
        Write-Log "    - driver_playstation_vr2_orig.dll (Sony backup): $(Test-Path $backupDll)"
    } else {
        Write-Log "Verification failed!" "ERROR"
        Write-Log "  - driver_playstation_vr2.dll: $verifyOriginal" "ERROR"
        Write-Log "  - driver_playstation_vr2_orig.dll: $verifyBackup" "ERROR"
        exit 1
    }

    # Step 5: Write log file
    Write-Log "Step 5: Writing installation log..."
    $logContent = @"
PSVR2 Toolkit Installation Log
==============================
Timestamp: $(Get-Date -Format "yyyy-MM-dd HH:mm:ss")
Steam Path: $steamPath
Driver Path: $driverPath
Source DLL: $sourceDllFullPath
Status: SUCCESS

Files:
- driver_playstation_vr2.dll (PSVR2 Toolkit)
- driver_playstation_vr2_orig.dll (Sony backup)

To restore the original driver, run: .\driver-restore.ps1
"@

    try {
        $logContent | Out-File -FilePath $logFile -Encoding UTF8 -Append
        Write-Log "  ✓ Log written to: $logFile"
    } catch {
        Write-Log "Failed to write log file: $_" "WARN"
    }

    Write-Log ""
    Write-Log "=== Installation Complete ===" "INFO"
    Write-Log "PSVR2 Toolkit driver has been successfully installed."
    Write-Log "Please restart SteamVR for changes to take effect."
    Write-Log ""

    exit 0

} catch {
    Write-Log "Unexpected error during installation: $_" "ERROR"
    Write-Log $_.ScriptStackTrace "ERROR"
    exit 1
}
