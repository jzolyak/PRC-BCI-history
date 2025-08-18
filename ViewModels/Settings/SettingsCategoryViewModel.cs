using CoreServices.Common;
using CoreServices.Models;
using CoreServices.Presets;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Input;
using ViewModels.Help;

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
            presetsService = ServiceLocator.PresetsService;
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
                var allValues = rawValue.Split('|');

                string? selectedValue = null;
                string? selectedDomainKey = null;

                foreach (var v in allValues)
                {
                    var parts = v.Split(';');
                    var valuePart = parts.FirstOrDefault(p => !p.StartsWith("domain=", StringComparison.OrdinalIgnoreCase));
                    var domainPart = parts.FirstOrDefault(p => p.StartsWith("domain=", StringComparison.OrdinalIgnoreCase));

                    if (!string.IsNullOrWhiteSpace(valuePart))
                    {
                        if (valuePart.StartsWith(key + "=", StringComparison.OrdinalIgnoreCase))
                        {
                            var eqSplit = valuePart.Split('=');
                            if (eqSplit.Length == 2)
                                selectedValue = eqSplit[1].Trim();
                        }
                        else
                        {
                            selectedValue = valuePart.Trim();
                        }
                    }

                    if (!string.IsNullOrWhiteSpace(domainPart))
                    {
                        var tokens = domainPart.Split('=');
                        if (tokens.Length == 2)
                            selectedDomainKey = tokens[1].Trim();
                    }

                    // ✅ Break once we found a domain + value pair
                    if (selectedValue != null && selectedDomainKey != null)
                        break;
                }

                List<string>? domainValues = null;
                if (!string.IsNullOrEmpty(selectedDomainKey))
                    domainValues = presetsService.GetDomainForKey(selectedDomainKey);

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
                    if (string.IsNullOrWhiteSpace(item.Header))
                        continue;

                    if (item.DomainValues != null && item.SelectedDomainKey != null)
                    {
                        // ✅ Save only selected value + domain reference
                        if (!string.IsNullOrWhiteSpace(item.Content))
                        {
                            dict[item.Header] = $"{item.Content};domain={item.SelectedDomainKey}";
                        }
                    }
                    else
                    {
                        // No domain → normal key=value
                        dict[item.Header] = item.Content ?? string.Empty;
                    }
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
            if (exitAction == null) return;
            exitAction(Constants.Settings);
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