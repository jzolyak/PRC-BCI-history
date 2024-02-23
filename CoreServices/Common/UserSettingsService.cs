using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;

namespace CoreServices.Common
{
    public class UserSettingsService
    {
        private ConcurrentDictionary<string, string> _userSettings;
        private string _settingsFileName;
        private string _defaultSettingsFileName;

        public UserSettingsService()
        {
            _userSettings = new ConcurrentDictionary<string, string>();
            _defaultSettingsFileName = System.IO.Path.GetDirectoryName(Assembly.GetEntryAssembly().Location) + "\\Settings\\DefaultSettings.txt";
            LoadSettings(_defaultSettingsFileName);
            _settingsFileName = System.IO.Path.GetDirectoryName(Assembly.GetEntryAssembly().Location) + "\\Settings\\UserSettings.txt";
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
            var keyValuePair = s.Split("=");
            if (keyValuePair.Length > 1)
            {
                if (_userSettings.ContainsKey(keyValuePair[0]))
                {
                    var existingvalue = _userSettings[keyValuePair[0]];
                    _userSettings.TryUpdate(keyValuePair[0], keyValuePair[1], existingvalue);
                }
                else
                {
                    _userSettings.TryAdd(keyValuePair[0], keyValuePair[1]);
                }
            }
        }

        public void SaveUserSettings(string key, string value)
        {
            if (_userSettings.ContainsKey(key))
            {
                _userSettings[key] = value;
                var allLines = _userSettings.Select(x => x.Key + "=" + x.Value);
                File.WriteAllLines(_settingsFileName, allLines);
            }
            else
            {
                _userSettings.TryAdd(key, value);
                File.AppendAllLines(_settingsFileName, new string[] { (key + "=" + value) });
            }
        }

        public string GetSettingsValue(string key)
        {
            if (_userSettings.IsEmpty || !_userSettings.ContainsKey(key))
            {
                Logger.Error(this, "Cannot Find Setting " + key); return null;
            }
            return _userSettings[key];
        }

        public Dictionary<string, string> GetSettings(string pattern)
        {
            if (_userSettings.IsEmpty) { Logger.Error(this, "Settings file is empty. Cannot Find Setting " + pattern); return null; }
            var matchedsettings = _userSettings.Where(x => x.Key.StartsWith(pattern + ";")).ToList();
            if (!matchedsettings.Any()) { Logger.Error(this, "Cannot Find Setting " + pattern); return null; }
            return matchedsettings.ToDictionary(y => y.Key, y => y.Value);
        }
    }
}
