using CoreServices.Common;
using CoreServices.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Input;
using ViewModels.Help;

namespace ViewModels.Settings
{
    public class SettingsDetailViewModel:ViewModelBase
    {
        private List<SettingsLineItemViewModel> settingsList;
        private string parentName;
        public List<SettingsLineItemViewModel> SettingsList { get { return settingsList; } set { settingsList = value; OnPropertyChanged(nameof(SettingsList)); } }

        public ICommand SaveCmd { get; set; }
        public ICommand ReturnCmd { get; set; }

        private Action<string> exitAction;
        private UserSettingsService userSettingsService;
        public SettingsDetailViewModel(Action<string> exitAction)
        {
            SaveCmd = new RelayCommand(SaveSettings);
            ReturnCmd = new RelayCommand(Return);
            this.exitAction = exitAction;
            userSettingsService = ServiceLocator.UserSettingsService;
        }

        public void UpdateSettingsList(string parentName)
        {
            SettingsList = null;
            this.parentName = parentName;
            var settings = userSettingsService.GetSettings(parentName);
            if (settings == null) return;
            SettingsList = settings.Select(x => { return new SettingsLineItemViewModel() { Header = ViewModelHelper.getKey(x.Key,parentName),Content = x.Value }; }).ToList() ;
        }

        private void SaveSettings(object obj)
        {
            if (settingsList != null && settingsList.Count > 0)
            {
                SettingsList.ForEach(settings => { if (settings != null){ userSettingsService.SaveUserSettings(parentName + ";" + settings.Header, settings.Content); } });
            }
        }

        private void Return(object obj)
        {
            if (exitAction == null) return;
            exitAction(Constants.Settings);
        }

    }
}
