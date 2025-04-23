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
    public class SettingsCategoryViewModel: ViewModelBase
    {

        private List<SettingsLineItemViewModel> settingsList;
        private string groupName;
        private string categoryName;
        private double _opacity;
        private object? _popupContentViewModel;
        private bool _showPopup;
        public List<SettingsLineItemViewModel> SettingsList { get { return settingsList; } set { settingsList = value; OnPropertyChanged(nameof(SettingsList)); } }
        private SettingsSaveStatusViewModel _settingsSaveStatusViewModel;
        public ICommand SaveCmd { get; set; }
        public ICommand ReturnCmd { get; set; }
        
        public string CategoryName { get { return categoryName; } set { categoryName = value;OnPropertyChanged(nameof(CategoryName)); } }
        public double Opacity { get => _opacity; set { _opacity = value; OnPropertyChanged(nameof(Opacity)); } }
        private Action<string> exitAction;
        private PresetsService presetsService;
        public bool ShowPopup { get => _showPopup; set { _showPopup = value; OnPropertyChanged(nameof(ShowPopup)); } }
        public object? PopupContentViewModel { get => _popupContentViewModel; set { _popupContentViewModel = value; OnPropertyChanged(nameof(PopupContentViewModel)); } }
        public SettingsCategoryViewModel(Action<string> exitAction)
        {
            SaveCmd = new RelayCommand(SaveSettings);
            ReturnCmd = new RelayCommand(Return);
            this.exitAction = exitAction;
            presetsService = ServiceLocator.PresetsService;
            _settingsSaveStatusViewModel = new SettingsSaveStatusViewModel(ClosePopup);
        }

        public void UpdateSettingsList(string groupName,string categoryName)
        {
            SettingsList = null;
            this.groupName = groupName;
            this.categoryName = categoryName;
            var settings = presetsService.GetSettings(groupName,categoryName);
            if (settings == null) return;
            settings = settings.OrderBy(s => s.Key).ToDictionary(s => s.Key, s => s.Value);
            SettingsList = settings.Where(x=>x.Key.ToLower().StartsWith("user")).ToList().Select(x => { return new SettingsLineItemViewModel() { Header = ViewModelHelper.getKey(x.Key,"user"), Content = x.Value }; }).ToList();
        }

        private void SaveSettings(object obj)
        {
            if (settingsList != null && settingsList.Count > 0)
            {
                try
                {
                    //SettingsList.ForEach(settings => { if (settings != null) { presetsService.SaveUserSettings(this.groupName, this.categoryName, "user", settings.Header, settings.Content); } });
                    foreach (var settings in settingsList)
                    {
                        if (settings != null)
                        {
                            presetsService.SaveUserSettings(this.groupName, this.categoryName, "user", settings.Header, settings.Content);
                        }
                    }

                    _settingsSaveStatusViewModel.SetStatus("Settings saved successfully.");
                }
                catch (Exception ex)
                {
                    _settingsSaveStatusViewModel.SetStatus($"Failed to save settings: {ex.Message}");
                }
               
            }
            else
            {
                _settingsSaveStatusViewModel.SetStatus("No settings to save.");
                
            }
            DisplayPopup(_settingsSaveStatusViewModel);
        }

        private void Return(object obj)
        {
            if (exitAction == null) return;
            exitAction(Constants.Settings);
        }

        private void DisplayPopup(object contentViewModel)
        {
            Opacity = .1;
            PopupContentViewModel = contentViewModel;
            ShowPopup = true;

        }
        private void ClosePopup()
        {
            Opacity = 1;
            PopupContentViewModel = null;
            ShowPopup = false;
            //if (_showorHideMainWindow != null) _showorHideMainWindow(true);
        }
    }
}
