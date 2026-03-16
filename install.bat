@echo off
REM PSVR2 Toolkit Easy Installer
REM This batch file wraps the PowerShell installation script for easier use

REM Change to script directory
cd /d "%~dp0"

echo ========================================
echo PSVR2 Toolkit Easy Installer
echo ========================================
echo.
echo Current directory: %CD%
echo.

REM Check for admin privileges
net session >nul 2>&1
if %errorLevel% neq 0 (
    echo ERROR: This script requires Administrator privileges.
    echo Please right-click on install.bat and select "Run as Administrator"
    echo.
    pause
    exit /b 1
)

echo Checking for driver DLL...
if exist "driver_playstation_vr2.dll" (
    set "DLL_PATH=driver_playstation_vr2.dll"
) else if exist "x64\Release\driver_playstation_vr2.dll" (
    set "DLL_PATH=x64\Release\driver_playstation_vr2.dll"
) else if exist "artifacts\driver_playstation_vr2.dll" (
    set "DLL_PATH=artifacts\driver_playstation_vr2.dll"
) else (
    echo ERROR: Could not find driver_playstation_vr2.dll
    echo.
    echo Files in current directory:
    dir /b *.dll 2>nul
    echo.
    echo Please ensure driver_playstation_vr2.dll is in the same folder as this script.
    echo.
    pause
    exit /b 1
)

echo Found driver DLL: %DLL_PATH%
echo.

REM Check for PowerShell script
if not exist "scripts\driver-install-full.ps1" (
    echo ERROR: Installation script not found!
    echo Expected location: scripts\driver-install-full.ps1
    echo.
    echo Please ensure the scripts folder is in the same directory as install.bat
    echo.
    pause
    exit /b 1
)

echo Starting installation...
echo.

REM Run the PowerShell installation script
powershell.exe -NoProfile -ExecutionPolicy Bypass -Command "& '%~dp0scripts\driver-install-full.ps1' -SourceDll '%DLL_PATH%'"

if %errorLevel% equ 0 (
    echo.
    echo ========================================
    echo Installation completed successfully!
    echo ========================================
    echo.
    echo Please restart SteamVR for changes to take effect.
    echo.
) else (
    echo.
    echo ========================================
    echo Installation failed!
    echo ========================================
    echo.
    echo Please check the error messages above.
    echo.
)

pause
