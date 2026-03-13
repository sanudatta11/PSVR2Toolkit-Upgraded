using System;
using System.IO;

namespace PSVR2Toolkit.App
{
    public static class AppConstants
    {
        public const int IPC_SERVER_PORT = 3364;
        public const int GAZE_UPDATE_INTERVAL_MS = 100;
        public const int IPC_CONNECTION_TIMEOUT_MS = 1000;
        public const int TRIGGER_TEST_DURATION_MS = 1000;
        public const int HAPTIC_TEST_DURATION_MS = 1000;
        public const ushort EXPECTED_IPC_VERSION = 5;
        public const int MAX_PROFILE_COUNT = 100;
        public const int MAX_JSON_FILE_SIZE_BYTES = 1024 * 1024; // 1MB

        public const string APP_NAME = "PSVR2 Toolkit";
        public const string APP_VERSION = "1.0.0-beta";
        public const string APP_FOLDER = "PSVR2Toolkit";
        public const string PROFILES_FILENAME = "TriggerProfiles.json";
        public const string PROFILES_FOLDER = "Resources";
        public const string CALIBRATION_FILENAME = "PSVR2Calibration.txt";
        public const string SETTINGS_FILENAME = "PSVR2ToolkitSettings.json";

        public static string CalibrationPath => Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
            CALIBRATION_FILENAME);

        /// <summary>
        /// Full path to the user settings file.
        /// Stored under %AppData%\PSVR2Toolkit\ following Windows conventions.
        /// </summary>
        public static string SettingsPath => Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            APP_FOLDER,
            SETTINGS_FILENAME);
    }
}
