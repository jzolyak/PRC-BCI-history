using CoreServices.Common;
using CoreServices.Models;
using System;
using System.Collections.Generic;
using System.Text;
using System.Windows.Input;

namespace ViewModels.Calibration
{
    public class DemoVideoViewModel : StartCalibrationViewModel
    {
        private bool _demoVideoPreference;
        private ICommand DemoVideoPrefChangeCmd  { get; set; }

        public bool DemoVideoPreference { get => _demoVideoPreference; set {  _demoVideoPreference = value; OnPropertyChanged(nameof(DemoVideoPreference)); ChangeVideoPreference(null); } }

        public DemoVideoViewModel(Action startCalibrationAction,Action closePopup) : base(startCalibrationAction,closePopup)
        {
            DemoVideoPrefChangeCmd = new RelayCommand(ChangeVideoPreference);
            _demoVideoPreference = false;
        }

        private void ChangeVideoPreference(object obj)
        {
            if (_demoVideoPreference)
            {
                ServiceLocator.UserSettingsService.SaveUserSettings(Constants.DemoVideo_Preference_Key, "false");
            }
            else
            {
                ServiceLocator.UserSettingsService.SaveUserSettings(Constants.DemoVideo_Preference_Key, "true");
            }
        }
    }
}
