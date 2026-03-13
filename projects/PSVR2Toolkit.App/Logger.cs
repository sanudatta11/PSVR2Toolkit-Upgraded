using System;
using System.IO;

namespace PSVR2Toolkit.App
{
    public enum LogLevel
    {
        Debug,
        Info,
        Warning,
        Error
    }

    public static class Logger
    {
        private static readonly object lockObject = new object();
        private static string? logFilePath;
        private static LogLevel minimumLevel = LogLevel.Info;

        static Logger()
        {
            try
            {
                var appDataPath = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                    "PSVR2Toolkit"
                );
                
                if (!Directory.Exists(appDataPath))
                {
                    Directory.CreateDirectory(appDataPath);
                }

                logFilePath = Path.Combine(appDataPath, $"app_{DateTime.Now:yyyyMMdd}.log");
            }
            catch
            {
                logFilePath = null;
            }
        }

        public static void SetMinimumLevel(LogLevel level)
        {
            minimumLevel = level;
        }

        public static void Debug(string message)
        {
            Log(LogLevel.Debug, message);
        }

        public static void Info(string message)
        {
            Log(LogLevel.Info, message);
        }

        public static void Warning(string message, Exception? ex = null)
        {
            Log(LogLevel.Warning, message, ex);
        }

        public static void Error(string message, Exception? ex = null)
        {
            Log(LogLevel.Error, message, ex);
        }

        private static void Log(LogLevel level, string message, Exception? ex = null)
        {
            if (level < minimumLevel)
                return;

            var timestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff");
            var logMessage = $"[{timestamp}] [{level}] {message}";
            
            if (ex != null)
            {
                logMessage += $"\nException: {ex.GetType().Name}: {ex.Message}\n{ex.StackTrace}";
            }

            System.Diagnostics.Debug.WriteLine(logMessage);

            if (logFilePath != null)
            {
                try
                {
                    lock (lockObject)
                    {
                        File.AppendAllText(logFilePath, logMessage + Environment.NewLine);
                    }
                }
                catch
                {
                }
            }
        }

        public static string? GetLogFilePath()
        {
            return logFilePath;
        }
    }
}
