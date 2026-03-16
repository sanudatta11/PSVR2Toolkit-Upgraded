using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Sockets;
using Microsoft.Win32;
using PSVR2Toolkit.CAPI;

namespace PSVR2Toolkit.App
{
    public enum HealthCheckStatus
    {
        Pass,
        Fail,
        Warning
    }

    public class HealthCheckItem
    {
        public string Name { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public HealthCheckStatus Status { get; set; }
        public string Message { get; set; } = string.Empty;
        public string FixSuggestion { get; set; } = string.Empty;
    }

    public class HealthReport
    {
        public List<HealthCheckItem> Items { get; set; } = new List<HealthCheckItem>();
        public bool AllPassed => Items.TrueForAll(item => item.Status == HealthCheckStatus.Pass);
        public int PassCount => Items.FindAll(item => item.Status == HealthCheckStatus.Pass).Count;
        public int FailCount => Items.FindAll(item => item.Status == HealthCheckStatus.Fail).Count;
        public int WarningCount => Items.FindAll(item => item.Status == HealthCheckStatus.Warning).Count;
    }

    public class HealthCheckService
    {

        public HealthReport RunHealthCheck()
        {
            var report = new HealthReport();

            report.Items.Add(CheckSteamInstallation());

            var steamPath = FindSteamPath();
            if (steamPath != null)
            {
                var driverPath = FindDriverPath(steamPath);
                if (driverPath != null)
                {
                    report.Items.Add(CheckOriginalDll(driverPath));
                    report.Items.Add(CheckToolkitDll(driverPath));
                }
                else
                {
                    report.Items.Add(new HealthCheckItem
                    {
                        Name = "PS VR2 Driver Location",
                        Description = "Locate PS VR2 driver directory",
                        Status = HealthCheckStatus.Fail,
                        Message = "Could not find PS VR2 driver directory",
                        FixSuggestion = "Install PS VR2 app from Steam"
                    });
                }
            }

            report.Items.Add(CheckIpcConnection());
            report.Items.Add(CheckHandshakeResult());
            report.Items.Add(CheckIpcVersion());

            return report;
        }

        private HealthCheckItem CheckSteamInstallation()
        {
            var steamPath = FindSteamPath();

            if (steamPath != null)
            {
                return new HealthCheckItem
                {
                    Name = "Steam Installation",
                    Description = "Verify Steam is installed",
                    Status = HealthCheckStatus.Pass,
                    Message = $"Found at: {steamPath}"
                };
            }

            return new HealthCheckItem
            {
                Name = "Steam Installation",
                Description = "Verify Steam is installed",
                Status = HealthCheckStatus.Fail,
                Message = "Steam installation not found",
                FixSuggestion = "Install Steam from https://store.steampowered.com/"
            };
        }

        private HealthCheckItem CheckOriginalDll(string driverPath)
        {
            var origDllPath = Path.Combine(driverPath, "driver_playstation_vr2_orig.dll");

            if (File.Exists(origDllPath))
            {
                return new HealthCheckItem
                {
                    Name = "Original Driver Backup",
                    Description = "Check driver_playstation_vr2_orig.dll exists",
                    Status = HealthCheckStatus.Pass,
                    Message = "Backup DLL found"
                };
            }

            return new HealthCheckItem
            {
                Name = "Original Driver Backup",
                Description = "Check driver_playstation_vr2_orig.dll exists",
                Status = HealthCheckStatus.Fail,
                Message = "Backup DLL not found",
                FixSuggestion = "Follow installation guide: rename driver_playstation_vr2.dll to driver_playstation_vr2_orig.dll"
            };
        }

        private HealthCheckItem CheckToolkitDll(string driverPath)
        {
            var toolkitDllPath = Path.Combine(driverPath, "driver_playstation_vr2.dll");

            if (File.Exists(toolkitDllPath))
            {
                return new HealthCheckItem
                {
                    Name = "Toolkit Driver",
                    Description = "Check driver_playstation_vr2.dll exists",
                    Status = HealthCheckStatus.Pass,
                    Message = "Toolkit DLL found"
                };
            }

            return new HealthCheckItem
            {
                Name = "Toolkit Driver",
                Description = "Check driver_playstation_vr2.dll exists",
                Status = HealthCheckStatus.Fail,
                Message = "Toolkit DLL not found",
                FixSuggestion = "Copy the toolkit driver_playstation_vr2.dll to the driver directory"
            };
        }

        private HealthCheckItem CheckIpcConnection()
        {
            try
            {
                using (var client = new TcpClient())
                {
                    var result = client.BeginConnect("127.0.0.1", AppConstants.IPC_SERVER_PORT, null, null);
                    var success = result.AsyncWaitHandle.WaitOne(TimeSpan.FromMilliseconds(AppConstants.IPC_CONNECTION_TIMEOUT_MS));

                    if (success)
                    {
                        client.EndConnect(result);
                        Logger.Info($"IPC connection successful on port {AppConstants.IPC_SERVER_PORT}");
                        return new HealthCheckItem
                        {
                            Name = "IPC Connection",
                            Description = "Check if driver IPC server is reachable",
                            Status = HealthCheckStatus.Pass,
                            Message = $"Connected to port {AppConstants.IPC_SERVER_PORT}"
                        };
                    }
                    else
                    {
                        Logger.Warning($"IPC connection timeout after {AppConstants.IPC_CONNECTION_TIMEOUT_MS}ms");
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.Error($"IPC connection failed: {ex.Message}", ex);
            }

            return new HealthCheckItem
            {
                Name = "IPC Connection",
                Description = "Check if driver IPC server is reachable",
                Status = HealthCheckStatus.Fail,
                Message = "Cannot connect to IPC server",
                FixSuggestion = "Start SteamVR with the PS VR2 headset connected"
            };
        }

        private HealthCheckItem CheckHandshakeResult()
        {
            var client = IpcClient.Instance();

            if (!client.IsConnected)
            {
                return new HealthCheckItem
                {
                    Name = "IPC Handshake",
                    Description = "Verify IPC handshake succeeded",
                    Status = HealthCheckStatus.Warning,
                    Message = "Not connected to IPC server",
                    FixSuggestion = "Start SteamVR to establish connection"
                };
            }

            switch (client.LastHandshakeResult)
            {
                case EHandshakeResult.Success:
                    return new HealthCheckItem
                    {
                        Name = "IPC Handshake",
                        Description = "Verify IPC handshake succeeded",
                        Status = HealthCheckStatus.Pass,
                        Message = "Handshake successful"
                    };

                case EHandshakeResult.Outdated:
                    return new HealthCheckItem
                    {
                        Name = "IPC Handshake",
                        Description = "Verify IPC handshake succeeded",
                        Status = HealthCheckStatus.Fail,
                        Message = "Client version outdated",
                        FixSuggestion = "Update PSVR2 Toolkit to the latest version"
                    };

                default:
                    return new HealthCheckItem
                    {
                        Name = "IPC Handshake",
                        Description = "Verify IPC handshake succeeded",
                        Status = HealthCheckStatus.Fail,
                        Message = "Handshake failed",
                        FixSuggestion = "Restart SteamVR and this application"
                    };
            }
        }

        private HealthCheckItem CheckIpcVersion()
        {
            var client = IpcClient.Instance();

            if (!client.IsConnected)
            {
                return new HealthCheckItem
                {
                    Name = "IPC Version",
                    Description = "Check IPC version compatibility",
                    Status = HealthCheckStatus.Warning,
                    Message = "Not connected to IPC server",
                    FixSuggestion = "Start SteamVR to check version"
                };
            }

            const ushort expectedVersion = AppConstants.EXPECTED_IPC_VERSION;
            var serverVersion = client.ServerIpcVersion;

            if (serverVersion == expectedVersion)
            {
                return new HealthCheckItem
                {
                    Name = "IPC Version",
                    Description = "Check IPC version compatibility",
                    Status = HealthCheckStatus.Pass,
                    Message = $"Version {serverVersion} (compatible)"
                };
            }

            if (serverVersion > expectedVersion)
            {
                return new HealthCheckItem
                {
                    Name = "IPC Version",
                    Description = "Check IPC version compatibility",
                    Status = HealthCheckStatus.Warning,
                    Message = $"Server v{serverVersion}, Client v{expectedVersion}",
                    FixSuggestion = "Update this app to support newer features"
                };
            }

            return new HealthCheckItem
            {
                Name = "IPC Version",
                Description = "Check IPC version compatibility",
                Status = HealthCheckStatus.Warning,
                Message = $"Server v{serverVersion}, Client v{expectedVersion}",
                FixSuggestion = "Update the driver to the latest version"
            };
        }

        private string? FindSteamPath()
        {
            // Try registry first
            try
            {
                using (var key = Registry.LocalMachine.OpenSubKey(@"SOFTWARE\Valve\Steam"))
                {
                    if (key != null)
                    {
                        var installPath = key.GetValue("InstallPath") as string;
                        if (!string.IsNullOrEmpty(installPath) && Directory.Exists(installPath))
                        {
                            Logger.Debug($"Found Steam via registry: {installPath}");
                            return installPath;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.Warning($"Registry access failed: {ex.Message}", ex);
            }

            // Try common paths
            var commonPaths = new[]
            {
                @"C:\Program Files (x86)\Steam",
                @"C:\Program Files\Steam",
                @"D:\Steam",
                @"E:\Steam"
            };

            foreach (var path in commonPaths)
            {
                if (Directory.Exists(path))
                {
                    return path;
                }
            }

            return null;
        }

        private string? FindDriverPath(string steamPath)
        {
            var relativePaths = new[]
            {
                @"steamapps\common\PS VR2\SteamVR_Plug-In\bin\win64",
                @"SteamApps\common\PS VR2\SteamVR_Plug-In\bin\win64"
            };

            // Check main Steam installation
            foreach (var relativePath in relativePaths)
            {
                var fullPath = Path.Combine(steamPath, relativePath);
                if (Directory.Exists(fullPath))
                {
                    return fullPath;
                }
            }

            // Check Steam library folders
            var libraryFolders = GetSteamLibraryFolders(steamPath);
            foreach (var libraryFolder in libraryFolders)
            {
                foreach (var relativePath in relativePaths)
                {
                    var fullPath = Path.Combine(libraryFolder, relativePath);
                    if (Directory.Exists(fullPath))
                    {
                        Logger.Debug($"Found PS VR2 driver in library folder: {fullPath}");
                        return fullPath;
                    }
                }
            }

            return null;
        }

        private List<string> GetSteamLibraryFolders(string steamPath)
        {
            var libraryFolders = new List<string>();

            try
            {
                var libraryFoldersVdf = Path.Combine(steamPath, "steamapps", "libraryfolders.vdf");
                if (!File.Exists(libraryFoldersVdf))
                {
                    libraryFoldersVdf = Path.Combine(steamPath, "SteamApps", "libraryfolders.vdf");
                }

                if (File.Exists(libraryFoldersVdf))
                {
                    var lines = File.ReadAllLines(libraryFoldersVdf);
                    foreach (var line in lines)
                    {
                        // Parse VDF format: "path"		"D:\\SteamLibrary"
                        if (line.Contains("\"path\""))
                        {
                            var parts = line.Split(new[] { '\t', ' ' }, StringSplitOptions.RemoveEmptyEntries);
                            if (parts.Length >= 2)
                            {
                                var path = parts[1].Trim('"').Replace("\\\\", "\\");
                                if (Directory.Exists(path))
                                {
                                    libraryFolders.Add(path);
                                    Logger.Debug($"Found Steam library folder: {path}");
                                }
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.Warning($"Failed to read Steam library folders: {ex.Message}", ex);
            }

            return libraryFolders;
        }
    }
}
