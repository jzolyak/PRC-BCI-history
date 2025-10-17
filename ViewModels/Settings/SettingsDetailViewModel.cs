using CoreServices.Common;
using CoreServices.Models;
using CoreServices.Presets;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Input;
using ViewModels.Help;

namespace ViewModels.Settings
{
    public class SettingsDetailViewModel : ViewModelBase
    {
        private string groupName;
        private Action<string> exitAction;
        private ObservableCollection<SettingsCategoryViewModel> settingsCategoryList;
        private PresetsService presetsService;
        public string GroupName { get { return groupName; } set { groupName = value; OnPropertyChanged(nameof(GroupName)); } }
        public ICommand ReturnCmd { get; set; }
        public bool IsInTablet { get; set; }
        public ObservableCollection<SettingsCategoryViewModel> SettingsCategoryList { get { return settingsCategoryList; } set { settingsCategoryList = value; OnPropertyChanged(nameof(SettingsCategoryList)); } }
        public SettingsDetailViewModel(Action<string> exitAction)
        {
            this.exitAction = exitAction;
            ReturnCmd = new RelayCommand(Return);
            this.presetsService = ServiceLocator.PresetsService;
            settingsCategoryList = new ObservableCollection<SettingsCategoryViewModel>();
        }

        public void UpdateGroup(string groupName)
        {
            settingsCategoryList = new ObservableCollection<SettingsCategoryViewModel>();
            GroupName = groupName;
            List<string> categories = presetsService.GetCategories(groupName);
            
            if (categories != null)
            {
                categories = categories.OrderBy(c => c).ToList();
                categories.ForEach(category =>
                {
                    var categoryViewModel = new SettingsCategoryViewModel(exitAction);
                    categoryViewModel.UpdateSettingsList(groupName, category);
                    settingsCategoryList.Add(categoryViewModel);
                }); 
            }
        }
        private void Return(object obj)
        {
            if (exitAction == null) return;
            exitAction(Constants.Settings);
        }

    }
}
