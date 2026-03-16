# PSVR2 Toolkit Installation Guide

## Quick Installation (Recommended)

1. **Extract** the downloaded ZIP file to a folder
2. **Right-click** on `install.bat` and select **"Run as Administrator"**
3. Wait for the installation to complete
4. **Restart SteamVR**

That's it! You're done.

---

## Manual Installation (Advanced)

If you prefer to install manually or the batch file doesn't work:

### Using PowerShell Script

1. Open PowerShell as Administrator
2. Navigate to the extracted folder
3. Run:
   ```powershell
   .\scripts\driver-install-full.ps1 -SourceDll .\driver_playstation_vr2.dll
   ```

### What Gets Installed

- **Enhanced Driver**: Replaces the Sony driver with PSVR2 Toolkit version
  - Original driver backed up as `driver_playstation_vr2_orig.dll`
  - Located in: `Steam\steamapps\common\PS VR2\SteamVR_Plug-In\bin\win64\`

- **SteamVR Settings Integration** (Optional): Adds in-VR settings menu
  - Access via: SteamVR Settings → PlayStation VR2

---

## Companion Application

Run `PSVR2Toolkit.App.exe` to access:
- Eye tracking calibration
- Gaze offset adjustment
- Trigger effect profiles
- Health check diagnostics
- Battery monitoring (when SteamVR is running)

**Note**: Requires .NET 8.0 Runtime (included in Windows 11, downloadable for Windows 10)

---

## Uninstalling

To restore the original Sony driver:

1. Run `scripts\driver-restore.ps1` as Administrator, **OR**
2. Manually restore the backup:
   - Navigate to: `Steam\steamapps\common\PS VR2\SteamVR_Plug-In\bin\win64\`
   - Delete `driver_playstation_vr2.dll`
   - Rename `driver_playstation_vr2_orig.dll` to `driver_playstation_vr2.dll`

---

## Troubleshooting

**"Access Denied" errors**: Make sure you run as Administrator

**"Steam not found"**: Use the `-SteamPath` parameter:
```powershell
.\scripts\driver-install-full.ps1 -SourceDll .\driver_playstation_vr2.dll -SteamPath "D:\Steam"
```

**Driver already installed**: Use `-Force` to reinstall:
```powershell
.\scripts\driver-install-full.ps1 -SourceDll .\driver_playstation_vr2.dll -Force
```

**Skip SteamVR settings**: Use `-SkipSteamVRSettings`:
```powershell
.\scripts\driver-install-full.ps1 -SourceDll .\driver_playstation_vr2.dll -SkipSteamVRSettings
```

---

## Support

For issues and support, visit: https://github.com/sanudatta11/PSVR2Toolkit-Upgraded
