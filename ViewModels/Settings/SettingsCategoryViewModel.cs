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

                var key = kvp.Key.Split(';')[1]; // "highlightoption"
                if (key == "domain") continue;

                string value = kvp.Value;
                string mainValue = value;
                List<string>? domainValues = null;
                string selectedDomainKey = null;

                // Split by semicolon to look for domain info
                var parts = value.Split(';');
                var mainPart = parts.FirstOrDefault(p => !p.StartsWith("domain="));
                var domainPart = parts.FirstOrDefault(p => p.StartsWith("domain="));
                

                if (!string.IsNullOrWhiteSpace(mainPart))
                    mainValue = mainPart;

                if (!string.IsNullOrWhiteSpace(domainPart))
                {
                    var domainKey = domainPart.Split('=')[1].Trim();
                    domainValues = presetsService.GetDomainForKey(domainKey);

                    selectedDomainKey = domainKey;
                }

                result.Add(new SettingsLineItemViewModel
                {
                    Header = key,
                    Content = mainValue,
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

                    // Append domain if present
                    string value = item.Content ?? string.Empty;

                    if (item.DomainValues != null && item.SelectedDomainKey != null)
                    {
                        value += $";domain={item.SelectedDomainKey}";
                    }

                    dict[item.Header] = value;
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