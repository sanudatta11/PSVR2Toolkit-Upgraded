using System;
using System.Collections.Generic;
using System.IO;
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
                    var jsonPath = Path.Combine(assemblyPath, "Resources", "TriggerProfiles.json");
                    
                    if (File.Exists(jsonPath))
                    {
                        var json = File.ReadAllText(jsonPath);
                        var loadedProfiles = JsonConvert.DeserializeObject<List<TriggerProfile>>(json);
                        if (loadedProfiles != null)
                        {
                            profiles = loadedProfiles;
                            return;
                        }
                    }
                }

                // Fallback to hardcoded profiles if file not found
                profiles = GetDefaultProfiles();
            }
            catch
            {
                // Use default profiles on error
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

        public List<TriggerProfile> GetProfiles()
        {
            return profiles;
        }

        public void ApplyProfile(TriggerProfile profile, EVRControllerType controller)
        {
            var ipcClient = IpcClient.Instance();

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
                    ipcClient.TriggerEffectDisable(controller);
                    break;
            }
        }
    }
}
