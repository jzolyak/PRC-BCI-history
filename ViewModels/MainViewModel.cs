using CoreServices.Common;
using CoreServices.Models;
using System;
using System.Collections.Generic;
using System.Text;
using ViewModels.Calibration;
using ViewModels.Connectivity;
using ViewModels.Help;
using ViewModels.Prediction;
using ViewModels.Settings;
using ViewModels.Startup;
using ViewModels.Validation;
using System.IO;
using System.Reflection;
using System.Xml;

namespace ViewModels
{
    public class MainViewModel:ViewModelBase
    {
        private string _title;
        private object _currentViewModel;
        private bool _isVisible;
        private double _opacity;
        private bool _isTopMost;

        public object CurrentViewModel { get { return _currentViewModel; } set { _currentViewModel = value; OnPropertyChanged(nameof(CurrentViewModel)); } }
        public bool IsVisible { get { return _isVisible; } set { _isVisible = value; OnPropertyChanged(nameof(IsVisible)); } }
        public double Opacity { get { return _opacity; } set { _opacity = value; OnPropertyChanged(nameof(Opacity)); } }
        public bool IsTopMost { get { return _isTopMost; } set { _isTopMost = value; OnPropertyChanged(nameof(IsTopMost)); } }

        public bool IsInTablet { get; set; }
        private ConnectivityViewModel _connectivityViewModel;
        private CalibrationViewModel _calibrationViewModel;
        private MainMenuViewModel _mainMenuViewModel;
        private ValidationViewModel _validationViewModel;
        private PredictionViewModel _predictionViewModel;
        private SettingsViewModel _settingsViewModel;
        private SettingsDetailViewModel _settingsDetailViewModel;
        private StartupViewModel _startupViewModel;
        private Action _closeAction;
        private ChildWindowSettings? _childWindowPref;
        private UserSettingsService _userSettingsService;

        public string Title { get { return _title; } set { _title = value; OnPropertyChanged(nameof(Title));} }

        private string BuildVersionString()
        {
            // Being deliberate here: We're only going to parse and display the version information from the
            // C:\Program Files\PRC-Saltillo\BCI\Version.xml if we're running a properly-deployed executable that's also located
            // in the C:\Program Files\PRC-Saltillo\BCI folder. If we're just running some random debug build from some other
            // location, then we could be running any old code, really, so we'll show the old "MVP.Beta.v1.0" test so we don't
            // mislead anyone.
            var localVersion = "Brain-Computer Interface - MVP.Beta v1.1.0.1 2025-02-20";   // Backup, just in case Version.xml isn't found/loaded
            var versionFilePath = Path.Combine(Path.GetDirectoryName(Assembly.GetEntryAssembly().Location), "Version.xml");

            XmlDocument xmlDocument = new XmlDocument();
            try
            {
                xmlDocument.Load(versionFilePath);
            }
            catch (Exception ex)
            {
                // Probably trying to load a non-existent Version.xml file from the Debug folder we're running from.
                return localVersion;
            }

            if (xmlDocument == null || xmlDocument.DocumentElement == null)
            {
                return localVersion;
            }

            // Optional code in case we ever want to use the value of the <Name> tag from Version.xml (but probably not,
            // since right now it just says "BCI").
            //var nameNodesList = xmlDocument.DocumentElement.GetElementsByTagName("Name");
            //if (nameNodesList.Count > 0 && nameNodesList[0] is not null && nameNodesList[0].InnerText.Length > 0)
            //{
            //    localVersion = nameNodesList[0].InnerText + " ";
            //}
            //else
            //{
            //    localVersion = "";
            //}
            localVersion = "Brain-Computer Interface ";


            var majorVersionNodesList = xmlDocument.DocumentElement.GetElementsByTagName("MajorVersion");
            if (majorVersionNodesList.Count > 0 && majorVersionNodesList[0] is not null && majorVersionNodesList[0].InnerText.Length > 0)
            {
                localVersion += majorVersionNodesList[0].InnerText + ".";
            }
            else
            {
                localVersion += "0.";
            }

            var minorVersionNodesList = xmlDocument.DocumentElement.GetElementsByTagName("MinorVersion");
            if (minorVersionNodesList.Count > 0 && minorVersionNodesList[0] is not null && minorVersionNodesList[0].InnerText.Length > 0)
            {
                if (minorVersionNodesList[0].InnerText.Length > 1)
                {
                    localVersion += minorVersionNodesList[0].InnerText + " ";
                }
                else
                {
                    localVersion += "0" + minorVersionNodesList[0].InnerText + " ";
                }
            }
            else
            {
                localVersion += "00";
            }

            var dateNodesList = xmlDocument.DocumentElement.GetElementsByTagName("Date");
            if (dateNodesList.Count > 0 && dateNodesList[0] is not null && dateNodesList[0].InnerText.Length > 0)
            {
                localVersion += dateNodesList[0].InnerText + " ";
            }

            var prereleaseNodesList = xmlDocument.DocumentElement.GetElementsByTagName("PreRelease");
            if (prereleaseNodesList.Count > 0 && prereleaseNodesList[0] is not null && prereleaseNodesList[0].InnerText.Length > 0)
            {
                localVersion += prereleaseNodesList[0].InnerText;
            }
            else
            {
                localVersion += "Release";
            }

            return localVersion;
        }

        public ChildWindowSettings? ChildWindowPref { get => _childWindowPref; set { _childWindowPref = value; OnPropertyChanged(nameof(ChildWindowPref)); } }

        public MainViewModel(Action closeAction)
        {
            Title = BuildVersionString();
            Opacity = 1;
            _mainMenuViewModel = new MainMenuViewModel(Open,Close);
            _connectivityViewModel = new ConnectivityViewModel(ExitToMainMenu, ShoworHideMainWindow);
            _mainMenuViewModel.HeadsetConnectionStatusViewModel = _connectivityViewModel.HeadsetConnectionStatusViewModel;
            _calibrationViewModel = new CalibrationViewModel(ExitToMainMenu, ShoworHideMainWindow, ShoworHideChildWindow);
            _settingsViewModel = new SettingsViewModel(Open, ExitToMainMenu, ShoworHideMainWindow);
            _settingsDetailViewModel = new SettingsDetailViewModel(Open);
            _predictionViewModel = new PredictionViewModel(ExitToMainMenu, ShoworHideMainWindow, ShoworHideChildWindow);
            _validationViewModel = new ValidationViewModel(ExitToMainMenu, ShoworHideMainWindow);
            CurrentViewModel = _mainMenuViewModel;
            _closeAction = closeAction;
            IsVisible = true;
            IsTopMost = true;
            ChildWindowPref = null;
        }

        private void Open(string mode)
        {
            if (string.IsNullOrEmpty(mode)) return;
            if (mode == Constants.Connectivity) { CurrentViewModel = _connectivityViewModel;
                _connectivityViewModel.IsInTablet = IsInTablet; }
            else if (mode == Constants.Calibration) { CurrentViewModel = _calibrationViewModel; _calibrationViewModel.IsInTablet = IsInTablet; }
            else if (mode == Constants.Validation) { CurrentViewModel = _validationViewModel; }
            else if (mode == Constants.Prediction) { _predictionViewModel.Refresh(); CurrentViewModel = _predictionViewModel; }
            else if (mode == Constants.Settings) { CurrentViewModel = _settingsViewModel; }
            else if (mode == Constants.ConnectivitySettings) { _settingsDetailViewModel.UpdateSettingsList(Constants.Connectivity); CurrentViewModel = _settingsDetailViewModel; }
            else if (mode == Constants.CalibrationSettings) { _settingsDetailViewModel.UpdateSettingsList(Constants.Calibration); CurrentViewModel = _settingsDetailViewModel; }
            else if (mode == Constants.PredictionSettings) { _settingsDetailViewModel.UpdateSettingsList(Constants.Prediction); CurrentViewModel = _settingsDetailViewModel; }
            else if (mode == Constants.ValidationSettings) { _settingsDetailViewModel.UpdateSettingsList(Constants.Validation); CurrentViewModel = _settingsDetailViewModel; }
            else if (mode == Constants.MiscSettings) { _settingsDetailViewModel.UpdateSettingsList(Constants.Misc); CurrentViewModel = _settingsDetailViewModel; }
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
           //acity = isVisible ? 1 : 0;
        }

        private void ShoworHideChildWindow(ChildWindowSettings childWindowSettings)
        {
            ChildWindowPref = childWindowSettings;
        }
    }
}
