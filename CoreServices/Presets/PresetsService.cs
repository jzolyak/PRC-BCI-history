using CoreServices.Files;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;

namespace CoreServices.Presets
{
    public class PresetsService
    {
        private ConcurrentDictionary<string, ConcurrentDictionary<string, ConcurrentDictionary<string, string>>> _presets;
        private string _settingsFileName;
        private string _defaultSettingsFileName;
        private string _settingsFilePath;
        private static PresetsService instance;
        public Dictionary<string, List<string>> GetAllDomains() => _domains;
        public event EventHandler<EventArgs> PresetsSettingsChanged;
        public static PresetsService Instance
        { get { if (instance == null) { instance = new PresetsService(); } return instance; } }
        public PresetsService()
        {
            _presets = new ConcurrentDictionary<string, ConcurrentDictionary<string, ConcurrentDictionary<string, string>>>();
            _defaultSettingsFileName = System.IO.Path.GetDirectoryName(Assembly.GetEntryAssembly().Location) + "\\Settings\\DefaultSettings.txt";
            _settingsFilePath = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData) + "\\BCI\\Settings";
            LoadSettings(_defaultSettingsFileName);
            _settingsFileName = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData) + "\\BCI\\Settings\\UserSettings.txt";
            LoadSettings(_settingsFileName);
            LoadDomains();
           
        }

        private void LoadSettings(string filename)
        {
            try
            {
                //Load settings from settings file
                if (!File.Exists(filename))
                {
                    Logger.Info(this, "Settings File does not exist");
                    return;
                }

                using (StreamReader sr = File.OpenText(filename))
                {
                    string s = "";
                    while ((s = sr.ReadLine()) != null)
                    {
                        ProcessSettings(s);
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.Error(this, ex.Message);
            }
        }

        private void ProcessSettings(string line)
        {
            if (string.IsNullOrWhiteSpace(line)) return;

            var parts = line.Split(';');
            if (parts.Length < 4) return;

            var grouping = parts[0];
            var category = parts[1];
            var type = parts[2];

            if (!_presets.ContainsKey(grouping))
                _presets[grouping] = new ConcurrentDictionary<string, ConcurrentDictionary<string, string>>();

            if (!_presets[grouping].ContainsKey(category))
                _presets[grouping][category] = new ConcurrentDictionary<string, string>();

            // Combine the rest of the parts (after first 3)
            var settingStr = string.Join(';', parts.Skip(3));

            // Split into key=value (only on first '=')
            var eqIndex = settingStr.IndexOf('=');
            if (eqIndex > 0)
            {
                var key = settingStr.Substring(0, eqIndex).Trim();
                var value = settingStr.Substring(eqIndex + 1).Trim();
                var dictKey = $"{type};{key}";

                _presets[grouping][category].AddOrUpdate(
                    dictKey,
                    value,                                      // if new
                    (k, oldVal) => value         // if exists, append
                );
            }
        }




        public void SaveUserSettings(string grouping, string category, string type, Dictionary<string, string> newSettings)
        {
            var allLines = File.Exists(_settingsFileName)
                ? File.ReadAllLines(_settingsFileName).ToList()
                : new List<string>();

            foreach (var kvp in newSettings)
            {
                string key = kvp.Key;
                string value = kvp.Value;

                string prefix = $"{grouping};{category};{type};{key}=";

                // find existing line index
                int index = allLines.FindIndex(line => line.StartsWith(prefix));

                if (index >= 0)
                {
                    // update line
                    allLines[index] = prefix + value;
                }
                else
                {
                    // add new line if not found
                    allLines.Add(prefix + value);
                }
                if (_presets.TryGetValue(grouping, out var categories) &&
              categories.TryGetValue(category, out var settings) &&
              settings.TryGetValue(type + ";" + key, out var value1))
                {
                    settings.TryUpdate(type + ";" + key, value, value1);
                }
            }

            File.WriteAllLines(_settingsFileName, allLines);

            PresetsSettingsChanged?.Invoke(this, EventArgs.Empty);
        }




        public string GetSettingsValue(string grouping, string category, string type, string key)
        {
            if (_presets.TryGetValue(grouping, out var categories) &&
                categories.TryGetValue(category, out var settings) &&
                settings.TryGetValue(type+";"+key, out var value))
            {
                return value;
            }

            Logger.Error(this, $"Cannot Find Setting {grouping};{category};{type};{key}");
            return null;
        }

        public Dictionary<string, string> GetSettings(string grouping, string category)
        {
            if (_presets.TryGetValue(grouping, out var categories) &&
                categories.TryGetValue(category, out var settings))
            {
                return settings.ToDictionary(kvp => kvp.Key, kvp => kvp.Value);
            }

            Logger.Error(this, $"Settings not found for Grouping: {grouping}, Category: {category}");
            return null;
        }

        public List<string> GetCategories(string grouping)
        {
            if (_presets.TryGetValue(grouping, out var categories) )
            {
                return categories.Keys.ToList();
            }

            Logger.Error(this, $"Settings not found for Grouping: {grouping}");
            return null;
        }

        private Dictionary<string, List<string>> _domains;

        public void LoadDomains(string domainFile = null)
        {
            _domains = new Dictionary<string, List<string>>();
            //domainFile ??= Path.Combine(Path.GetDirectoryName(_settingsFileName), "domain.txt");
            domainFile ??= System.IO.Path.GetDirectoryName(Assembly.GetEntryAssembly().Location) + "\\Settings\\domain.txt";

            if (!File.Exists(domainFile)) return;

            foreach (var line in File.ReadAllLines(domainFile))
            {
                if (string.IsNullOrWhiteSpace(line)) continue;

                var parts = line.Split('=', 2);
                if (parts.Length == 2)
                {
                    var domainKey = parts[0].Trim();
                    var domainValues = parts[1]
                        .Split(new[] { ';', ',' }, StringSplitOptions.RemoveEmptyEntries)
                        .Select(v => v.Trim())
                        .ToList();

                    if (domainValues.Count > 0)
                        _domains[domainKey] = domainValues;
                }
            }
        }

        public List<string>? GetDomainForKey(string domainKey)
        {
            var allDomains = GetAllDomains();
            if (allDomains.TryGetValue(domainKey, out var values))
                return values;
            return null;
        }

    }
}
