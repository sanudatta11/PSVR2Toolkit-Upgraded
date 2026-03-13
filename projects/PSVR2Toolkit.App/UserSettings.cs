using System;
using System.IO;
using Newtonsoft.Json;

namespace PSVR2Toolkit.App
{
    /// <summary>
    /// Persists user preferences to <see cref="AppConstants.SettingsPath"/> across app restarts.
    /// All properties default to "off" / neutral so the first launch is no-op.
    /// </summary>
    public class UserSettings
    {
        // ── Gaze-cursor settings ────────────────────────────────────────────────
        [JsonProperty("gazeCursorEnabled")]
        public bool GazeCursorEnabled { get; set; } = false;

        [JsonProperty("cursorSensitivity")]
        public double CursorSensitivity { get; set; } = 1.0;

        [JsonProperty("cursorSmoothing")]
        public double CursorSmoothing { get; set; } = 0.3;

        // ── Persistence helpers ─────────────────────────────────────────────────
        public static UserSettings Load()
        {
            try
            {
                var path = AppConstants.SettingsPath;
                if (!File.Exists(path))
                    return new UserSettings();

                var fileInfo = new FileInfo(path);
                if (fileInfo.Length > AppConstants.MAX_JSON_FILE_SIZE_BYTES)
                {
                    Logger.Warning($"Settings file too large ({fileInfo.Length} bytes). Using defaults.");
                    return new UserSettings();
                }

                var json = File.ReadAllText(path);
                return JsonConvert.DeserializeObject<UserSettings>(json) ?? new UserSettings();
            }
            catch (Exception ex)
            {
                Logger.Error($"Failed to load user settings: {ex.Message}", ex);
                return new UserSettings();
            }
        }

        public void Save()
        {
            try
            {
                var path = AppConstants.SettingsPath;
                var dir = Path.GetDirectoryName(path);
                if (!string.IsNullOrEmpty(dir))
                    Directory.CreateDirectory(dir);
                var json = JsonConvert.SerializeObject(this, Formatting.Indented);
                File.WriteAllText(path, json);
            }
            catch (Exception ex)
            {
                Logger.Error($"Failed to save user settings: {ex.Message}", ex);
            }
        }
    }
}
