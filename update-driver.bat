@echo off
REM PSVR2 Toolkit Driver Update Script - Batch Wrapper
REM This batch file automatically builds and installs the PSVR2 driver

REM Check for admin privileges
net session >nul 2>&1
if %errorLevel% neq 0 (
    echo Requesting Administrator privileges...
    echo.

    REM Re-launch as admin
    powershell.exe -NoProfile -Command "Start-Process -FilePath '%~f0' -Verb RunAs"
    exit /b
)

echo ========================================
echo PSVR2 Toolkit Driver Update
echo ========================================
echo Running with Administrator privileges
echo.
echo This script will:
echo   1. Build the PSVR2 Toolkit driver (Release)
echo   2. Find your PS VR2 installation
echo   3. Preserve original Sony driver (first time only)
echo   4. Install/update PSVR2 Toolkit wrapper driver
echo.
echo Press any key to continue or Ctrl+C to cancel...
pause >nul

REM Run PowerShell script with execution policy bypass
powershell.exe -NoProfile -ExecutionPolicy Bypass -File "%~dp0update-driver.ps1"

if %errorlevel% neq 0 (
    echo.
    echo Script failed with error code: %errorlevel%
    pause
)

exit /b %errorlevel%
