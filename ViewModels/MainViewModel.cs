using CoreServices.Models;
using System;
using System.Collections.Generic;
using System.Text;
using ViewModels.Calibration;
using ViewModels.Connectivity;
using ViewModels.Help;
using ViewModels.Prediction;
using ViewModels.Settings;
using ViewModels.Validation;

namespace ViewModels
{
    public class MainViewModel:ViewModelBase
    {
        private string _title;
        private object _currentViewModel;
        private bool _isVisible;
        public object CurrentViewModel { get { return _currentViewModel; } set { _currentViewModel = value; OnPropertyChanged(nameof(CurrentViewModel)); } }
        public bool IsVisible { get { return _isVisible; } set { _isVisible = value; OnPropertyChanged(nameof(IsVisible)); } }
        private ConnectivityViewModel _connectivityViewModel;
        private CalibrationViewModel _calibrationViewModel;
        private MainMenuViewModel _mainMenuViewModel;
        private ValidationViewModel _validationViewModel;
        private PredictionViewModel _predictionViewModel;
        private SettingsViewModel _settingsViewModel;
        private Action _closeAction;
        private ChildWindowSettings? _childWindowPref;

        public string Title { get { return _title; } set { _title = value; OnPropertyChanged(nameof(Title));} }

        public ChildWindowSettings? ChildWindowPref { get => _childWindowPref; set { _childWindowPref = value; OnPropertyChanged(nameof(ChildWindowPref)); } }

        public MainViewModel(Action closeAction)
        {
            Title = "Brain Computer Interface";
            _mainMenuViewModel = new MainMenuViewModel(Open,Close);
            _connectivityViewModel = new ConnectivityViewModel(ExitToMainMenu, ShoworHideMainWindow);
            _mainMenuViewModel.HeadsetConnectionStatusViewModel = _connectivityViewModel.HeadsetConnectionStatusViewModel;
            _calibrationViewModel = new CalibrationViewModel(ExitToMainMenu, ShoworHideMainWindow, ShoworHideChildWindow);
            _settingsViewModel = new SettingsViewModel(ExitToMainMenu, ShoworHideMainWindow);
            _predictionViewModel = new PredictionViewModel(ExitToMainMenu, ShoworHideMainWindow);
            _validationViewModel = new ValidationViewModel(ExitToMainMenu, ShoworHideMainWindow);
            CurrentViewModel = _mainMenuViewModel;
            _closeAction = closeAction;
            IsVisible = true;
            ChildWindowPref = null;
        }

        private void Open(string mode)
        {
            if (string.IsNullOrEmpty(mode)) return;
            if (mode == Constants.Connectivity) { CurrentViewModel = _connectivityViewModel; }
            else if (mode == Constants.Calibration) { CurrentViewModel = _calibrationViewModel; }
            else if (mode == Constants.Validation) { CurrentViewModel = _validationViewModel; }
            else if (mode == Constants.Prediction) { CurrentViewModel = _validationViewModel; }
            else if (mode == Constants.Settings) { CurrentViewModel = _validationViewModel; }
        }
        private void Close()
        {
            if (_closeAction != null) _closeAction();
        }
        private void ExitToMainMenu()
        {
            CurrentViewModel = _mainMenuViewModel;
        }

        private void ShoworHideMainWindow(bool isVisible)
        {
            IsVisible = isVisible;        
        }

        private void ShoworHideChildWindow(ChildWindowSettings childWindowSettings)
        {
            ChildWindowPref = childWindowSettings;
        }
    }
}
