using CoreServices.Models;
using System;
using System.Collections.Generic;
using System.Text;
using ViewModels.Calibration;
using ViewModels.Connectivity;
using ViewModels.Prediction;
using ViewModels.Settings;
using ViewModels.Validation;

namespace ViewModels
{
    public class MainViewModel:ViewModelBase
    {
        private string _title;
        private object _currentViewModel;

        public object CurrentViewModel { get { return _currentViewModel; } set { _currentViewModel = value; OnPropertyChanged(nameof(CurrentViewModel)); } }
        private ConnectivityViewModel _connectivityViewModel;
        private CalibrationViewModel _calibrationViewModel;
        private MainMenuViewModel _mainMenuViewModel;
        private ValidationViewModel _validationViewModel;
        private PredictionViewModel _predictionViewModel;
        private SettingsViewModel _settingsViewModel;

        public string Title { get { return _title; } set { _title = value; OnPropertyChanged(nameof(Title));} }

        public MainViewModel()
        {
            Title = "Brain Computer Interface";
            _mainMenuViewModel = new MainMenuViewModel(Open,Close);
            _connectivityViewModel = new ConnectivityViewModel(ExitToMainMenu);
            _calibrationViewModel = new CalibrationViewModel(ExitToMainMenu);
            _settingsViewModel = new SettingsViewModel(ExitToMainMenu);
            _predictionViewModel = new PredictionViewModel(ExitToMainMenu);
            _validationViewModel = new ValidationViewModel(ExitToMainMenu);
            CurrentViewModel = _mainMenuViewModel;
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
           
        }
        private void ExitToMainMenu()
        {
            CurrentViewModel = _mainMenuViewModel;
        }
    }
}
