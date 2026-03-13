using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using Newtonsoft.Json;
using PSVR2Toolkit.CAPI;

namespace PSVR2Toolkit.App
{
    public class TriggerProfile
    {
        [JsonProperty("name")]
        public string Name { get; set; } = string.Empty;

        [JsonProperty("description")]
        public string Description { get; set; } = string.Empty;

        [JsonProperty("type")]
        public string Type { get; set; } = string.Empty;

        [JsonProperty("position")]
        public byte Position { get; set; }

        [JsonProperty("strength")]
        public byte Strength { get; set; }

        [JsonProperty("startPosition")]
        public byte StartPosition { get; set; }

        [JsonProperty("endPosition")]
        public byte EndPosition { get; set; }

        [JsonProperty("startStrength")]
        public byte StartStrength { get; set; }

        [JsonProperty("endStrength")]
        public byte EndStrength { get; set; }

        [JsonProperty("amplitude")]
        public byte Amplitude { get; set; }

        [JsonProperty("frequency")]
        public byte Frequency { get; set; }
    }

    public class TriggerProfileService
    {
        private List<TriggerProfile> profiles = new List<TriggerProfile>();

        public TriggerProfileService()
        {
            LoadProfiles();
        }

        private void LoadProfiles()
        {
            try
            {
                // Try to load from Resources folder
                var assemblyPath = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
                if (assemblyPath != null)
                {
                    var jsonPath = Path.Combine(assemblyPath, AppConstants.PROFILES_FOLDER, AppConstants.PROFILES_FILENAME);

                    if (File.Exists(jsonPath))
                    {
                        // Validate file size
                        var fileInfo = new FileInfo(jsonPath);
                        if (fileInfo.Length > AppConstants.MAX_JSON_FILE_SIZE_BYTES)
                        {
                            Logger.Warning($"Profile file too large: {fileInfo.Length} bytes. Using defaults.");
                            profiles = GetDefaultProfiles();
                            return;
                        }

                        var json = File.ReadAllText(jsonPath);
                        var loadedProfiles = JsonConvert.DeserializeObject<List<TriggerProfile>>(json);

                        if (loadedProfiles != null && ValidateProfiles(loadedProfiles))
                        {
                            profiles = loadedProfiles;
                            Logger.Info($"Loaded {profiles.Count} trigger profiles from {jsonPath}");
                            return;
                        }
                        else
                        {
                            Logger.Warning("Profile validation failed. Using defaults.");
                        }
                    }
                    else
                    {
                        Logger.Info($"Profile file not found at {jsonPath}. Using defaults.");
                    }
                }

                // Fallback to hardcoded profiles if file not found
                profiles = GetDefaultProfiles();
            }
            catch (JsonException ex)
            {
                Logger.Error($"JSON parsing error in trigger profiles: {ex.Message}", ex);
                profiles = GetDefaultProfiles();
            }
            catch (Exception ex)
            {
                Logger.Error($"Failed to load trigger profiles: {ex.Message}", ex);
                profiles = GetDefaultProfiles();
            }
        }

        private List<TriggerProfile> GetDefaultProfiles()
        {
            return new List<TriggerProfile>
            {
                new TriggerProfile
                {
                    Name = "Off",
                    Description = "No resistance or feedback",
                    Type = "Off"
                },
                new TriggerProfile
                {
                    Name = "Soft",
                    Description = "Light feedback for casual games",
                    Type = "Feedback",
                    Position = 0,
                    Strength = 40
                },
                new TriggerProfile
                {
                    Name = "Arcade",
                    Description = "Snappy trigger with defined pull point",
                    Type = "Weapon",
                    StartPosition = 20,
                    EndPosition = 100,
                    Strength = 200
                },
                new TriggerProfile
                {
                    Name = "Realistic",
                    Description = "Gradual resistance like a real trigger",
                    Type = "SlopeFeedback",
                    StartPosition = 20,
                    EndPosition = 100,
                    StartStrength = 10,
                    EndStrength = 255
                },
                new TriggerProfile
                {
                    Name = "Rumble",
                    Description = "Vibration effect",
                    Type = "Vibration",
                    Position = 0,
                    Amplitude = 200,
                    Frequency = 20
                }
            };
        }

        private bool ValidateProfiles(List<TriggerProfile> profilesToValidate)
        {
            if (profilesToValidate.Count == 0 || profilesToValidate.Count > AppConstants.MAX_PROFILE_COUNT)
            {
                Logger.Warning($"Invalid profile count: {profilesToValidate.Count}");
                return false;
            }

            foreach (var profile in profilesToValidate)
            {
                if (string.IsNullOrWhiteSpace(profile.Name))
                {
                    Logger.Warning("Profile has empty name");
                    return false;
                }

                if (string.IsNullOrWhiteSpace(profile.Type))
                {
                    Logger.Warning($"Profile '{profile.Name}' has empty type");
                    return false;
                }

                var validTypes = new[] { "off", "feedback", "weapon", "vibration", "slopefeedback" };
                if (!validTypes.Contains(profile.Type.ToLower()))
                {
                    Logger.Warning($"Profile '{profile.Name}' has invalid type: {profile.Type}");
                    return false;
                }
            }

            return true;
        }

        public List<TriggerProfile> GetProfiles()
        {
            return profiles;
        }

        public void ApplyProfile(TriggerProfile profile, EVRControllerType controller)
        {
            if (profile == null)
            {
                Logger.Warning("Attempted to apply null profile");
                return;
            }

            var ipcClient = IpcClient.Instance();
            if (ipcClient == null)
            {
                Logger.Error("IPC client instance is null");
                return;
            }

            try
            {
                Logger.Info($"Applying profile '{profile.Name}' to {controller}");

                switch (profile.Type.ToLower())
                {
                    case "off":
                        ipcClient.TriggerEffectDisable(controller);
                        break;

                    case "feedback":
                        ipcClient.TriggerEffectFeedback(controller, profile.Position, profile.Strength);
                        break;

                    case "weapon":
                        ipcClient.TriggerEffectWeapon(controller, profile.StartPosition, profile.EndPosition, profile.Strength);
                        break;

                    case "vibration":
                        ipcClient.TriggerEffectVibration(controller, profile.Position, profile.Amplitude, profile.Frequency);
                        break;

                    case "slopefeedback":
                        ipcClient.TriggerEffectSlopeFeedback(controller, profile.StartPosition, profile.EndPosition, profile.StartStrength, profile.EndStrength);
                        break;

                    default:
                        Logger.Warning($"Unknown profile type: {profile.Type}");
                        ipcClient.TriggerEffectDisable(controller);
                        break;
                }
            }
            catch (Exception ex)
            {
                Logger.Error($"Failed to apply profile '{profile.Name}': {ex.Message}", ex);
            }
        }
    }
}
