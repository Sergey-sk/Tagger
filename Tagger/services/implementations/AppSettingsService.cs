using System.IO;
using System.Text.Json;
using System.Windows;

namespace Tagger.services.implementations
{
    public class AppSettingsService
    {
        private string SettingsPath
        {
            get
            {
                string appDataPath = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
                string appFolderPath = Path.Combine(appDataPath, "Tagger");
                Directory.CreateDirectory(appFolderPath);
                return Path.Combine(appFolderPath, "app-user-settings.json");
            }
        }

        private Dictionary<string, string> _settings = new();

        public AppSettingsService()
        {
            LoadSettings();
        }

        private void LoadSettings()
        {
            try
            {
                if (File.Exists(SettingsPath))
                {
                    var json = File.ReadAllText(SettingsPath);
                    _settings = JsonSerializer.Deserialize<Dictionary<string, string>>(json) ?? new();
                }
            }
            catch { }
        }

        public string GetSetting(string key, string defaultValue = "")
        {
            return _settings.TryGetValue(key, out var value) ? value : defaultValue;
        }

        public void SetSetting(string key, string value)
        {
            _settings[key] = value;
            SaveSettings();
        }

        public void SaveSettings()
        {
            try
            {
                var json = JsonSerializer.Serialize(_settings, new JsonSerializerOptions { WriteIndented = true });
                File.WriteAllText(SettingsPath, json);
            }
            catch { }
        }
    }
}
