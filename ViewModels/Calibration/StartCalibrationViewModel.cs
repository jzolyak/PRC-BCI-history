using System;
using System.Collections.Generic;
using System.Text;
using System.Windows.Input;

namespace ViewModels.Calibration
{
    public class StartCalibrationViewModel:PopupCloseViewModel
    {
        public ICommand StartCalibrationCommand { get; set; }
        protected Action _startCalibrationAction;
        public StartCalibrationViewModel(Action startCalibrationAction,Action closePopup):base(closePopup)
        {
            StartCalibrationCommand = new RelayCommand(StartCalibration);
            _startCalibrationAction = startCalibrationAction;

        }

        protected void StartCalibration(object obj)
        {
            if (_startCalibrationAction == null) return;
            _startCalibrationAction();
        }
    }
}
