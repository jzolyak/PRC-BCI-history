using CoreServices.Models;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Text;
using System.Windows.Input;

namespace ViewModels.Settings
{
    public class SettingsViewModel : ExitToMainMenuViewModel
    {

        private Action<string> _openCommandAction;
        private double _opacity;
        private object? _popupContentViewModel;
        private ObservableCollection<SettingGroupViewModel> _groupingsCollection;
        public ICommand OpenCmd { get; set; }
        private bool _showPopup;
        private SettingsSaveStatusViewModel _settingsSaveStatusViewModel;
        public ObservableCollection<SettingGroupViewModel> GroupingsCollection { get { return _groupingsCollection; } set { _groupingsCollection = value; OnPropertyChanged(nameof(GroupingsCollection)); } }
        public double Opacity { get => _opacity; set { _opacity = value; OnPropertyChanged(nameof(Opacity)); } }
        public object? PopupContentViewModel { get => _popupContentViewModel; set { _popupContentViewModel = value; OnPropertyChanged(nameof(PopupContentViewModel)); } }
        public bool ShowPopup { get => _showPopup; set { _showPopup = value; OnPropertyChanged(nameof(ShowPopup)); } }
        

        public SettingsViewModel(Action<string> openCommandAction,Action exitAction,Action<bool> showorHideMainWindow) : base(exitAction, showorHideMainWindow)
        {
            _openCommandAction = openCommandAction;
            OpenCmd = new RelayCommand(Open);
            _groupingsCollection = new ObservableCollection<SettingGroupViewModel>();
            _groupingsCollection.Add( new SettingGroupViewModel(openCommandAction,"Calibration", Constants.CalibrationSettings));
            _groupingsCollection.Add(new SettingGroupViewModel(openCommandAction, "Prediction", Constants.PredictionSettings));
            _groupingsCollection.Add(new SettingGroupViewModel(openCommandAction, "Validation", Constants.ValidationSettings));
            _groupingsCollection.Add(new SettingGroupViewModel(openCommandAction, "Flash Stimulus", Constants.FlashSettings));
            _groupingsCollection.Add(new SettingGroupViewModel(openCommandAction, "Connectivity", Constants.ConnectivitySettings));
            _groupingsCollection.Add(new SettingGroupViewModel(openCommandAction, "Miscellaneous", Constants.MiscSettings));
            _groupingsCollection.Add(new SettingGroupViewModel(openCommandAction, "System", Constants.SystemSettings));
            _groupingsCollection.Add(new SettingGroupViewModel(openCommandAction, "Logging", Constants.LoggingSettings));
            PopupContentViewModel = null;
            _settingsSaveStatusViewModel = new SettingsSaveStatusViewModel(ClosePopup);
            Opacity = 1;

        }
        
        private void Open(object obj)
        {
            if (obj == null || string.IsNullOrEmpty(obj.ToString()) || _openCommandAction == null) return;
            _openCommandAction(obj.ToString());
        }

        private void ClosePopup()
        {
            Opacity = 1;
            PopupContentViewModel = null;
            ShowPopup = false;
            if (_showorHideMainWindow != null) _showorHideMainWindow(true);
        }

        private void ShowSettingSaveStatus()
        {
            //if (_showorHideMainWindow != null) _showorHideMainWindow(false);
            DisplayPopup(_settingsSaveStatusViewModel);
        }

        private void DisplayPopup(object contentViewModel)
        {
            Opacity = .1;
            PopupContentViewModel = contentViewModel;
            ShowPopup = true;

        }
    }
}
