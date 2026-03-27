using CoreServices.Presets;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Input;

namespace ViewModels.Settings
{
    public class SettingsCategoryViewModel : ViewModelBase
    {
        private List<SettingsLineItemViewModel> settingsList;
        private string groupName;
        private string categoryName;
        private double opacity;
        private object? popupContentViewModel;
        private bool showPopup;
        private SettingsSaveStatusViewModel statusViewModel;
        private Action<string> exitAction;
        private PresetsService presetsService;

        public SettingsCategoryViewModel(Action<string> exitAction)
        {
            this.exitAction = exitAction;
            presetsService = PresetsService.Instance;
            statusViewModel = new SettingsSaveStatusViewModel(ClosePopup);

            SaveCmd = new RelayCommand(SaveSettings);
            ReturnCmd = new RelayCommand(Return);
        }

        public List<SettingsLineItemViewModel> SettingsList { get => settingsList; set { settingsList = value; OnPropertyChanged(nameof(SettingsList)); } }
        public string CategoryName { get => categoryName; set { categoryName = value; OnPropertyChanged(nameof(CategoryName)); } }
        public double Opacity { get => opacity; set { opacity = value; OnPropertyChanged(nameof(Opacity)); } }
        public object? PopupContentViewModel { get => popupContentViewModel; set { popupContentViewModel = value; OnPropertyChanged(nameof(PopupContentViewModel)); } }
        public bool ShowPopup { get => showPopup; set { showPopup = value; OnPropertyChanged(nameof(ShowPopup)); } }
        public ICommand SaveCmd { get; set; }
        public ICommand ReturnCmd { get; set; }

        public void UpdateSettingsList(string group, string category)
        {
            groupName = group;
            categoryName = category;

            var settings = presetsService.GetSettings(group, category);
            var result = new List<SettingsLineItemViewModel>();

            foreach (var kvp in settings)
            {
                if (!kvp.Key.StartsWith("user;")) continue;

                var key = kvp.Key.Split(';')[1];
                if (key == "domain") continue;

                string rawValue = kvp.Value;
                string? selectedValue = rawValue;
                string? selectedDomainKey = null;

                // Extract domain if present
                if (rawValue.Contains(";domain=", StringComparison.OrdinalIgnoreCase))
                {
                    var parts = rawValue.Split(new[] { ";domain=" }, StringSplitOptions.None);
                    selectedValue = parts[0].Trim();
                    selectedDomainKey = parts[1].Trim();
                }

                List<string>? domainValues = null;
                if (!string.IsNullOrEmpty(selectedDomainKey))
                {
                    domainValues = presetsService.GetDomainForKey(selectedDomainKey);

                    // match exact casing
                    if (domainValues != null && selectedValue != null)
                    {
                        var match = domainValues.FirstOrDefault(v => v.Equals(selectedValue, StringComparison.OrdinalIgnoreCase));
                        if (match != null)
                            selectedValue = match;
                    }
                }

                result.Add(new SettingsLineItemViewModel
                {
                    Header = key,
                    Content = selectedValue ?? string.Empty,
                    DomainValues = domainValues,
                    SelectedDomainKey = selectedDomainKey
                });
            }

            SettingsList = result;
        }

        private void SaveSettings(object obj)
        {
            if (settingsList == null || settingsList.Count == 0)
            {
                statusViewModel.SetStatus("No settings to save.");
                DisplayPopup(statusViewModel);
                return;
            }

            try
            {
                var dict = new Dictionary<string, string>();

                foreach (var item in settingsList)
                {
                    if (string.IsNullOrWhiteSpace(item.Header)) continue;

                    // Save only value, omit domain in user file
                    dict[item.Header] = item.Content ?? string.Empty;
                }

                presetsService.SaveUserSettings(groupName, categoryName, "user", dict);
                statusViewModel.SetStatus("Settings saved successfully.");
            }
            catch (Exception ex)
            {
                statusViewModel.SetStatus($"Failed to save settings: {ex.Message}");
            }

            DisplayPopup(statusViewModel);
        }

        private void Return(object obj)
        {
            exitAction?.Invoke("Settings");
        }

        private void DisplayPopup(object content)
        {
            Opacity = 0.1;
            PopupContentViewModel = content;
            ShowPopup = true;
        }

        private void ClosePopup()
        {
            Opacity = 1;
            PopupContentViewModel = null;
            ShowPopup = false;
        }
    }
}
