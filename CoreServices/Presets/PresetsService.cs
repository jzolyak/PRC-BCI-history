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

        private void ProcessSettings(string s)
        {
            if (string.IsNullOrEmpty(s))
            {
                return;
            }
            var keyValuePair = s.Split(";");
            if (keyValuePair.Length > 3)
            {
                string grouping = keyValuePair[0];
                string category = keyValuePair[1];
                string type = keyValuePair[2];  
                string keyValue= keyValuePair[3];
                string[] actualKeyValue = keyValue.Split("=");
                if (!_presets.ContainsKey(grouping))
                    _presets[grouping] = new ConcurrentDictionary<string, ConcurrentDictionary<string, string>>();
                if (!_presets[grouping].ContainsKey(category))
                    _presets[grouping][category] = new ConcurrentDictionary<string, string>();
                _presets[grouping][category][type + ";" + actualKeyValue[0]]= actualKeyValue[1];

            }
        }

        public void SaveUserSettings(string grouping, string category, string type, string key, string value)
        {
            //if (!_presets.ContainsKey(grouping))
            //    _presets[grouping] = new ConcurrentDictionary<string, ConcurrentDictionary<string, string>>();

            //if (!_presets[grouping].ContainsKey(category))
            //    _presets[grouping][category] = new ConcurrentDictionary<string, string>();

            //_presets[grouping][category].TryAdd(type+";"+key, value);

            //var line = $"{grouping};{category};{type};{key}={value}";
            //File.AppendAllLines(_settingsFileName, new[] { line });

            if (!_presets.ContainsKey(grouping))
                _presets[grouping] = new ConcurrentDictionary<string, ConcurrentDictionary<string, string>>();

            if (!_presets[grouping].ContainsKey(category))
                _presets[grouping][category] = new ConcurrentDictionary<string, string>();

            _presets[grouping][category][type + ";" + key] = value;

            var newLineKey = $"{grouping};{category};{type};{key}";
            var newLine = $"{newLineKey}={value}";

            var lines = File.Exists(_settingsFileName)
                ? File.ReadAllLines(_settingsFileName).ToList()
                : new List<string>();

            bool updated = false;

            for (int i = 0; i < lines.Count; i++)
            {
                if (lines[i].StartsWith(newLineKey + "=", StringComparison.OrdinalIgnoreCase))
                {
                    lines[i] = newLine;
                    updated = true;
                    break;
                }
            }

            if (!updated)
            {
                lines.Add(newLine);
            }

            File.WriteAllLines(_settingsFileName, lines);
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
    }
}
