using CoreServices.Common;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;

namespace CoreServices.Presets
{
    //  Hybrid structure for ordered + fast lookup settings
    public class OrderedSettings
    {
        public List<KeyValuePair<string, string>> OrderedList { get; } = new();
        public Dictionary<string, string> Lookup { get; } = new();

        public void AddOrUpdate(string key, string value)
        {
            if (Lookup.ContainsKey(key))
            {
                Lookup[key] = value;
                var index = OrderedList.FindIndex(kvp => kvp.Key == key);
                if (index >= 0)
                    OrderedList[index] = new KeyValuePair<string, string>(key, value);
            }
            else
            {
                Lookup[key] = value;
                OrderedList.Add(new KeyValuePair<string, string>(key, value));
            }
        }

        public bool TryGetValue(string key, out string value) => Lookup.TryGetValue(key, out value);
    }

    //  Hybrid structure for ordered + fast lookup domains
    public class OrderedDomain
    {
        public List<string> OrderedList { get; } = new();
        public HashSet<string> Lookup { get; } = new(StringComparer.OrdinalIgnoreCase);

        public void Add(string value)
        {
            if (!Lookup.Contains(value))
            {
                Lookup.Add(value);
                OrderedList.Add(value);
            }
        }

        public bool Contains(string value) => Lookup.Contains(value);
    }

    public class PresetsService
    {
        private ConcurrentDictionary<string, ConcurrentDictionary<string, OrderedSettings>> _presets;
        private Dictionary<string, OrderedDomain> _domains;

        private string _settingsFileName;
        private string _defaultSettingsFileName;
        private string _settingsFilePath;

        private static PresetsService instance;

        public event EventHandler<EventArgs> PresetsSettingsChanged;

        public static PresetsService Instance
        {
            get
            {
                if (instance == null)
                    instance = new PresetsService();
                return instance;
            }
        }

        public PresetsService()
        {
            _presets = new ConcurrentDictionary<string, ConcurrentDictionary<string, OrderedSettings>>();
            _defaultSettingsFileName = Path.GetDirectoryName(Assembly.GetEntryAssembly().Location) + "\\Settings\\DefaultSettings.txt";
            _settingsFilePath = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData) + "\\BCI\\Settings";

            LoadSettings(_defaultSettingsFileName);
            _settingsFileName = Path.Combine(_settingsFilePath, "UserSettings.txt");
            LoadSettings(_settingsFileName);

            LoadDomains();
        }

        //  Load settings preserving file order
        private void LoadSettings(string filename)
        {
            try
            {
                if (!File.Exists(filename))
                {
                    Logger.Info(this, $"Settings File {filename} does not exist");
                    return;
                }

                foreach (var line in File.ReadAllLines(filename))
                {
                    ProcessSettings(line);
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
                _presets[grouping] = new ConcurrentDictionary<string, OrderedSettings>();

            if (!_presets[grouping].ContainsKey(category))
                _presets[grouping][category] = new OrderedSettings();

            var settingStr = string.Join(';', parts.Skip(3));
            var eqIndex = settingStr.IndexOf('=');

            if (eqIndex > 0)
            {
                var key = settingStr.Substring(0, eqIndex).Trim();
                var value = settingStr.Substring(eqIndex + 1).Trim();
                var dictKey = $"{type};{key}";

                _presets[grouping][category].AddOrUpdate(dictKey, value);
            }
        }

        //  Save settings while preserving file order
        public void SaveUserSettings(string grouping, string category, string type, Dictionary<string, string> newSettings)
        {
            var defaultLines = File.Exists(_defaultSettingsFileName)
                ? File.ReadAllLines(_defaultSettingsFileName).ToList()
                : new List<string>();

            var userLines = File.Exists(_settingsFileName)
                ? File.ReadAllLines(_settingsFileName).ToList()
                : new List<string>();

            foreach (var kvp in newSettings)
            {
                string key = $"{grouping};{category};{type};{kvp.Key}";
                string value = kvp.Value;
                string line = $"{key}={value}";

                bool updated = false;

                for (int i = 0; i < defaultLines.Count; i++)
                {
                    if (defaultLines[i].StartsWith(key + "=", StringComparison.OrdinalIgnoreCase))
                    {
                        defaultLines[i] = line;
                        updated = true;
                        break;
                    }
                }

                if (!updated)
                {
                    for (int i = 0; i < userLines.Count; i++)
                    {
                        if (userLines[i].StartsWith(key + "=", StringComparison.OrdinalIgnoreCase))
                        {
                            userLines[i] = line;
                            updated = true;
                            break;
                        }
                    }
                }

                if (!updated)
                    userLines.Add(line);

                if (_presets.TryGetValue(grouping, out var categories) &&
                    categories.TryGetValue(category, out var settings))
                {
                    settings.AddOrUpdate($"{type};{kvp.Key}", value);
                }
            }

            File.WriteAllLines(_defaultSettingsFileName, defaultLines);
            File.WriteAllLines(_settingsFileName, userLines);

            PresetsSettingsChanged?.Invoke(this, EventArgs.Empty);
        }

        //  Get single value
        public string GetSettingsValue(string grouping, string category, string type, string key)
        {
            if (_presets.TryGetValue(grouping, out var categories) &&
                categories.TryGetValue(category, out var settings) &&
                settings.TryGetValue(type + ";" + key, out var value))
            {
                return value;
            }

            Logger.Error(this, $"Cannot Find Setting {grouping};{category};{type};{key}");
            return null;
        }

        //  Get unordered dictionary (fast lookups)
        public Dictionary<string, string> GetSettings(string grouping, string category)
        {
            if (_presets.TryGetValue(grouping, out var categories) &&
                categories.TryGetValue(category, out var settings))
            {
                return new Dictionary<string, string>(settings.Lookup);
            }

            Logger.Error(this, $"Settings not found for Grouping: {grouping}, Category: {category}");
            return null;
        }

        //  Get ordered list (UI binding)
        public List<KeyValuePair<string, string>> GetOrderedSettings(string grouping, string category)
        {
            if (_presets.TryGetValue(grouping, out var categories) &&
                categories.TryGetValue(category, out var settings))
            {
                return new List<KeyValuePair<string, string>>(settings.OrderedList);
            }

            return new List<KeyValuePair<string, string>>();
        }

        //  Get categories
        public List<string> GetCategories(string grouping)
        {
            if (_presets.TryGetValue(grouping, out var categories))
            {
                return categories.Keys.ToList();
            }

            Logger.Error(this, $"Settings not found for Grouping: {grouping}");
            return null;
        }

        //  Load domains preserving order
        public void LoadDomains(string domainFile = null)
        {
            _domains = new Dictionary<string, OrderedDomain>();
            domainFile ??= Path.GetDirectoryName(Assembly.GetEntryAssembly().Location) + "\\Settings\\domain.txt";

            if (!File.Exists(domainFile)) return;

            foreach (var line in File.ReadAllLines(domainFile))
            {
                if (string.IsNullOrWhiteSpace(line)) continue;

                var parts = line.Split('=', 2);
                if (parts.Length == 2)
                {
                    var domainKey = parts[0].Trim();
                    var values = parts[1]
                        .Split(new[] { ';', ',' }, StringSplitOptions.RemoveEmptyEntries)
                        .Select(v => v.Trim());

                    if (!_domains.ContainsKey(domainKey))
                        _domains[domainKey] = new OrderedDomain();

                    foreach (var val in values)
                        _domains[domainKey].Add(val);
                }
            }
        }

        //  Get all domains (ordered for UI)
        public Dictionary<string, List<string>> GetAllDomains()
        {
            return _domains.ToDictionary(
                kvp => kvp.Key,
                kvp => new List<string>(kvp.Value.OrderedList));
        }

        //  Get specific domain values (ordered)
        public List<string>? GetDomainForKey(string domainKey)
        {
            if (_domains.TryGetValue(domainKey, out var domain))
                return new List<string>(domain.OrderedList);

            return null;
        }

        //  Fast check if value exists in domain
        public bool DomainContainsValue(string domainKey, string value)
        {
            return _domains.TryGetValue(domainKey, out var domain) && domain.Contains(value);
        }
    }
}
