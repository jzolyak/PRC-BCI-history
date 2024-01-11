using CoreServices.Common;
using CoreServices.Headset;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ViewModels.Startup
{
    public class StartupViewModel:ExitToMainMenuViewModel
    {
        private object _currentViewModel;
        private HeadsetService _headsetService;
        public object CurrentViewModel { get { return _currentViewModel; } set { _currentViewModel = value; OnPropertyChanged(nameof(CurrentViewModel));  } }    
        private FirstScreenViewModel _firstScreenViewModel;
        private HeadsetStartupStatusViewModel _headsetStartupStatusViewModel;
        private SettingsService _userSettingsService;
        public StartupViewModel(Action exitAction, Action<bool> showorHideMainWindow,Action closeAction):base(exitAction, showorHideMainWindow)
        {
            _firstScreenViewModel = new FirstScreenViewModel(ConnectToHeadset, closeAction);
            CurrentViewModel = _firstScreenViewModel;
            _headsetService = ServiceLocator.HeadsetService;
            _headsetStartupStatusViewModel = new HeadsetStartupStatusViewModel(NextStepAfterConnection, ShowFirstScreen);
            _userSettingsService= ServiceLocator.UserSettingsService;
        }

        private async void ConnectToHeadset()
        {
            if(!_headsetService.Headset.IsConnected())
            {
                await _headsetService.ConnectAsync();
            }
            if(_headsetService.Headset.IsConnected())
            {
                //open success
                _headsetStartupStatusViewModel.UpdateStatus("BCI Headset found and headset connected",true);
            }
            else
            {
                //not connected.
                _headsetStartupStatusViewModel.UpdateStatus("BCI Headset not found. Please turn on BCI headset", false);
            }
            CurrentViewModel = _headsetStartupStatusViewModel;
        }

        private void ShowFirstScreen()
        {
            CurrentViewModel = _firstScreenViewModel;
        }

        private void NextStepAfterConnection()
        {
            //if its firttime show firsttime connection screens

            //if not go to main menu
            _exitAction();
        }

    }
}
