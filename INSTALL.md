# PSVR2 Toolkit Installation Guide

## Installation Steps

### 1. Locate Your PS VR2 Driver Folder

Navigate to your Steam installation and find the PS VR2 driver folder:
```
Steam\steamapps\common\PlayStation VR2 App\SteamVR_Plug-In\bin\win64\
```

Common Steam locations:
- `C:\Program Files (x86)\Steam\`
- `D:\Steam\`
- `E:\Steam\`

### 2. Rename the Original Driver

In the `win64` folder, you'll find `driver_playstation_vr2.dll`

**IMPORTANT:** The toolkit driver is a wrapper that extends the original Sony driver. You must rename the original:
1. Right-click on `driver_playstation_vr2.dll`
2. Select "Rename"
3. Change the name to `driver_playstation_vr2_orig.dll`

**Both files must be present for the toolkit to work!**

### 3. Install the Toolkit Driver

1. Download and extract the PSVR2 Toolkit release ZIP
2. Copy `driver_playstation_vr2.dll` from the release package
3. Paste it into the `win64` folder

You should now have **both files** in the folder:
- `driver_playstation_vr2.dll` (Toolkit wrapper)
- `driver_playstation_vr2_orig.dll` (Original Sony driver)

### 4. Install the Companion App

1. Copy all files from the release package to a permanent location (e.g., `C:\Program Files\PSVR2Toolkit\`)
2. Keep these files together:
   - `PSVR2Toolkit.App.exe`
   - `PSVR2Toolkit.App.dll`
   - `PSVR2Toolkit.App.runtimeconfig.json`
   - `PSVR2Toolkit.App.deps.json`
   - `PSVR2Toolkit.IPC.dll`
   - `Newtonsoft.Json.dll`

### 5. Restart SteamVR

Close and restart SteamVR for the changes to take effect.

---

## Using the Companion App

### Launching the App

Run `PSVR2Toolkit.App.exe` from where you installed it.

**Requirements:**
- .NET 8.0 Runtime (included in Windows 11, [download for Windows 10](https://dotnet.microsoft.com/download/dotnet/8.0))
- SteamVR must be running for most features to work

### Features

#### 1. **Eye Tracking Calibration**
- Adjusts gaze offset for better eye tracking accuracy
- Click "Calibrate" and follow on-screen instructions
- Calibration data is saved automatically

#### 2. **Trigger Effect Profiles**
- Customize DualSense controller trigger resistance
- Choose from presets: Off, Soft, Medium, Rigid, Vibration
- Changes apply immediately to connected controllers

#### 3. **Battery Monitoring**
- View battery levels for left and right controllers
- Shows charging status
- Real-time updates when SteamVR is running

#### 4. **Health Check**
- Verifies IPC server connection
- Checks driver installation status
- Displays current settings and configuration

#### 5. **Settings**
- Adjust gaze offset manually (X, Y, Z coordinates)
- Configure trigger profiles per controller
- Save/load custom configurations

### Tips

- **Keep the app running** in the background for real-time adjustments
- **Calibrate regularly** if you notice eye tracking drift
- **Check Health Status** if features aren't working

---

## Uninstalling

To restore the original Sony driver:

1. Navigate to: `Steam\steamapps\common\PlayStation VR2 App\SteamVR_Plug-In\bin\win64\`
2. Delete `driver_playstation_vr2.dll`
3. Rename `driver_playstation_vr2_orig.dll` to `driver_playstation_vr2.dll`
4. Restart SteamVR

---

## Troubleshooting

**Driver not loading:**
- Verify the DLL is in the correct folder
- Check that you backed up the original file
- Restart SteamVR completely (close from system tray)

**Companion app won't start:**
- Install .NET 8.0 Runtime
- Run as Administrator if needed
- Check Windows Event Viewer for error details

**Eye tracking not working:**
- Ensure SteamVR is running
- Run the Health Check in the companion app
- Try recalibrating eye tracking

**Battery info not showing:**
- SteamVR must be running
- Controllers must be connected and turned on
- Wait a few seconds for data to populate

---

## Support

For issues and support:
- GitHub Issues: https://github.com/sanudatta11/PSVR2Toolkit-Upgraded/issues
- Discord: https://discord.gg/dPsfJhsGwb
