using System;
using System.IO;
using System.Text.Json;

namespace SonnissBrowser
{
    public sealed class AppSettingsStore
    {
        private readonly string _settingsDir;
        private readonly string _settingsPath;

        private sealed class AppSettings
        {
            public string? ExportPresetFolder { get; set; }
            public string? LastRootFolder { get; set; }   // ✅ add
            public bool IsDarkMode { get; set; } = true;  // Default to dark mode
        }

        public string? LoadLastRootFolder()
        {
            try
            {
                if (!File.Exists(_settingsPath)) return null;
                var json = File.ReadAllText(_settingsPath);
                var s = JsonSerializer.Deserialize<AppSettings>(json);
                return string.IsNullOrWhiteSpace(s?.LastRootFolder) ? null : s!.LastRootFolder;
            }
            catch { return null; }
        }

        public void SaveLastRootFolder(string? folder)
        {
            try
            {
                Directory.CreateDirectory(_settingsDir);

                AppSettings s;
                if (File.Exists(_settingsPath))
                {
                    var jsonOld = File.ReadAllText(_settingsPath);
                    s = JsonSerializer.Deserialize<AppSettings>(jsonOld) ?? new AppSettings();
                }
                else
                {
                    s = new AppSettings();
                }

                s.LastRootFolder = string.IsNullOrWhiteSpace(folder) ? null : folder.Trim();

                var json = JsonSerializer.Serialize(s, new JsonSerializerOptions { WriteIndented = true });
                File.WriteAllText(_settingsPath, json);
            }
            catch { }
        }
        
        public AppSettingsStore(string appFolderName = "RakSound")
        {
            _settingsDir = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                appFolderName
            );
            _settingsPath = Path.Combine(_settingsDir, "settings.json");
        }

        public string? LoadExportPresetFolder()
        {
            try
            {
                if (!File.Exists(_settingsPath)) return null;
                var json = File.ReadAllText(_settingsPath);
                var s = JsonSerializer.Deserialize<AppSettings>(json);
                return string.IsNullOrWhiteSpace(s?.ExportPresetFolder) ? null : s!.ExportPresetFolder;
            }
            catch { return null; }
        }

        public void SaveExportPresetFolder(string? folder)
        {
            try
            {
                Directory.CreateDirectory(_settingsDir);
                var s = new AppSettings { ExportPresetFolder = string.IsNullOrWhiteSpace(folder) ? null : folder.Trim() };
                var json = JsonSerializer.Serialize(s, new JsonSerializerOptions { WriteIndented = true });
                File.WriteAllText(_settingsPath, json);
            }
            catch { }
        }

        public bool LoadIsDarkMode()
        {
            try
            {
                if (!File.Exists(_settingsPath)) return true; // Default to dark
                var json = File.ReadAllText(_settingsPath);
                var s = JsonSerializer.Deserialize<AppSettings>(json);
                return s?.IsDarkMode ?? true;
            }
            catch { return true; }
        }

        public void SaveIsDarkMode(bool isDarkMode)
        {
            try
            {
                Directory.CreateDirectory(_settingsDir);

                AppSettings s;
                if (File.Exists(_settingsPath))
                {
                    var jsonOld = File.ReadAllText(_settingsPath);
                    s = JsonSerializer.Deserialize<AppSettings>(jsonOld) ?? new AppSettings();
                }
                else
                {
                    s = new AppSettings();
                }

                s.IsDarkMode = isDarkMode;

                var json = JsonSerializer.Serialize(s, new JsonSerializerOptions { WriteIndented = true });
                File.WriteAllText(_settingsPath, json);
            }
            catch { }
        }
    }
}