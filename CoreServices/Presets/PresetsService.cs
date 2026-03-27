using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;

namespace CoreServices.Presets
{
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
        private readonly ConcurrentDictionary<string, ConcurrentDictionary<string, OrderedSettings>> _presets;
        private Dictionary<string, OrderedDomain> _domains;

        private readonly string _settingsFilePath;
        private readonly string _defaultSettingsFileName;
        private readonly string _settingsFileName;

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

            _defaultSettingsFileName = Path.Combine(
                Path.GetDirectoryName(Assembly.GetEntryAssembly().Location)!,
                "Settings",
                "DefaultSettings.txt");

            _settingsFilePath = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "BCI",
                "Settings");

            _settingsFileName = Path.Combine(_settingsFilePath, "UserSettings.txt");

            LoadSettings(_defaultSettingsFileName);

            LoadUserSettings(_settingsFileName);
            LoadDomains();
        }

        private void LoadSettings(string filename)
        {
            try
            {
                if (!File.Exists(filename))
                    return;

                foreach (var line in File.ReadAllLines(filename))
                    ProcessSettings(line);
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

            var settingStr = string.Join(';', parts.Skip(3)).Trim();
            var eqIndex = settingStr.IndexOf('=');
            if (eqIndex <= 0) return;

            var key = settingStr[..eqIndex].Trim();
            var value = settingStr[(eqIndex + 1)..].Trim();

            var dictKey = $"{type.ToLowerInvariant()};{key.ToLowerInvariant()}";
            _presets[grouping][category].AddOrUpdate(dictKey, value);
        }

        private void LoadUserSettings(string filename)
        {
            try
            {
                if (!File.Exists(filename))
                    return;

                foreach (var line in File.ReadAllLines(filename))
                    ProcessUserSettings(line);
            }
            catch (Exception ex)
            {
                Logger.Error(this, ex.Message);
            }
        }

        private void ProcessUserSettings(string line)
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

            var settingStr = string.Join(';', parts.Skip(3)).Trim();
            var eqIndex = settingStr.IndexOf('=');
            if (eqIndex <= 0) return;

            var key = settingStr[..eqIndex].Trim();
            var rawValue = settingStr[(eqIndex + 1)..].Trim();
            var dictKey = $"{type.ToLowerInvariant()};{key.ToLowerInvariant()}";

            string value = rawValue;

            int domainIndex = value.IndexOf(";domain=", StringComparison.OrdinalIgnoreCase);
            if (domainIndex >= 0)
            {
                int afterDomain = value.IndexOf(';', domainIndex + 1);
                if (afterDomain > 0)
                    value = value.Substring(0, afterDomain).TrimEnd(';', ' ');
            }
            else
            {
                int firstExtra = value.IndexOf(';');
                if (firstExtra > 0)
                    value = value[..firstExtra].Trim();
            }

            int spaceOrSemi = value.IndexOfAny(new[] { ';', ' ' });
            if (spaceOrSemi > 0)
                value = value[..spaceOrSemi].Trim();

            if (_presets[grouping][category].TryGetValue(dictKey, out var existingValue))
            {
                if (!value.Contains("domain=", StringComparison.OrdinalIgnoreCase) &&
                    existingValue.Contains("domain=", StringComparison.OrdinalIgnoreCase))
                {
                    var domainPart = existingValue.Substring(
                        existingValue.IndexOf(";domain=", StringComparison.OrdinalIgnoreCase));
                    value = $"{value}{domainPart}";
                }
            }

            _presets[grouping][category].AddOrUpdate(dictKey, value);
        }

        public void SaveUserSettings(string grouping, string category, string type, Dictionary<string, string> newSettings)
        {
            var userLines = File.Exists(_settingsFileName)
                ? File.ReadAllLines(_settingsFileName).ToList()
                : new List<string>();

            foreach (var kvp in newSettings)
            {
                string dictKey = $"{type};{kvp.Key}";
                string value = kvp.Value;
                string line = $"{grouping};{category};{type};{kvp.Key}={value}";

                bool updated = false;
                for (int i = 0; i < userLines.Count; i++)
                {
                    if (userLines[i].StartsWith($"{grouping};{category};{type};{kvp.Key}=", StringComparison.OrdinalIgnoreCase))
                    {
                        userLines[i] = line;
                        updated = true;
                        break;
                    }
                }

                if (!updated)
                    userLines.Add(line);

                if (_presets.TryGetValue(grouping, out var categories) &&
                    categories.TryGetValue(category, out var settings))
                {
                    if (settings.TryGetValue(dictKey, out var existingValue) &&
                        existingValue.Contains("domain=", StringComparison.OrdinalIgnoreCase))
                    {
                        value = $"{value}{existingValue.Substring(existingValue.IndexOf(";domain=", StringComparison.OrdinalIgnoreCase))}";
                    }

                    settings.AddOrUpdate(dictKey, value);
                }
            }

            File.WriteAllLines(_settingsFileName, userLines);
            PresetsSettingsChanged?.Invoke(this, EventArgs.Empty);
        }

        public string GetSettingsValue(string grouping, string category, string type, string key)
        {
            if (_presets.TryGetValue(grouping, out var categories) &&
                categories.TryGetValue(category, out var settings) &&
                settings.TryGetValue($"{type};{key}", out var value))
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
                return new Dictionary<string, string>(settings.Lookup);

            Logger.Error(this, $"Settings not found for Grouping: {grouping}, Category: {category}");
            return null;
        }

        public List<KeyValuePair<string, string>> GetOrderedSettings(string grouping, string category)
        {
            if (_presets.TryGetValue(grouping, out var categories) &&
                categories.TryGetValue(category, out var settings))
                return new List<KeyValuePair<string, string>>(settings.OrderedList);

            return new List<KeyValuePair<string, string>>();
        }

        public List<string> GetCategories(string grouping)
        {
            if (_presets.TryGetValue(grouping, out var categories))
                return categories.Keys.ToList();

            Logger.Error(this, $"Settings not found for Grouping: {grouping}");
            return null;
        }

        public void LoadDomains(string domainFile = null)
        {
            _domains = new Dictionary<string, OrderedDomain>();
            domainFile ??= Path.Combine(
                Path.GetDirectoryName(Assembly.GetEntryAssembly().Location)!,
                "Settings",
                "domain.txt");

            if (!File.Exists(domainFile)) return;

            foreach (var line in File.ReadAllLines(domainFile))
            {
                if (string.IsNullOrWhiteSpace(line)) continue;

                var parts = line.Split('=', 2);
                if (parts.Length == 2)
                {
                    var domainKey = parts[0].Trim();
                    var values = parts[1].Split(new[] { ';', ',' }, StringSplitOptions.RemoveEmptyEntries)
                        .Select(v => v.Trim());

                    if (!_domains.ContainsKey(domainKey))
                        _domains[domainKey] = new OrderedDomain();

                    foreach (var val in values)
                        _domains[domainKey].Add(val);
                }
            }
        }

        public Dictionary<string, List<string>> GetAllDomains()
            => _domains.ToDictionary(kvp => kvp.Key, kvp => new List<string>(kvp.Value.OrderedList));

        public List<string>? GetDomainForKey(string domainKey)
            => _domains.TryGetValue(domainKey, out var domain) ? new List<string>(domain.OrderedList) : null;

        public bool DomainContainsValue(string domainKey, string value)
            => _domains.TryGetValue(domainKey, out var domain) && domain.Contains(value);
    }
}
