@echo off
REM PSVR2 Toolkit Easy Installer
REM This batch file wraps the PowerShell installation script for easier use

echo ========================================
echo PSVR2 Toolkit Easy Installer
echo ========================================
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
    echo Please ensure the DLL is in the same folder as this script.
    echo.
    pause
    exit /b 1
)

echo Found driver DLL: %DLL_PATH%
echo.
echo Starting installation...
echo.

REM Run the PowerShell installation script
powershell.exe -NoProfile -ExecutionPolicy Bypass -File "scripts\driver-install-full.ps1" -SourceDll "%DLL_PATH%"

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
