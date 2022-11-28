using CoreServices.Calibration;
using CoreServices.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Input;

namespace ViewModels.Calibration
{
    public class CalibrationStopQueryViewModel : ViewModelBase
    {
        public ICommand KeepCommand { get; set; }
        public ICommand DiscardCommand { get; set; }
        private Action<UIInstruction> _resultAction;

        public CalibrationStopQueryViewModel(Action<UIInstruction> resultAction)
        {
            KeepCommand = new RelayCommand(KeepCalibrationSet);
            DiscardCommand = new RelayCommand(DiscardCalibrationSet);
            _resultAction = resultAction;
        }

        private void KeepCalibrationSet(Object obj)
        {
            _resultAction(new UIInstruction { Type = UIType.CalibrationStopQuery, Result = true });
        }

        private void DiscardCalibrationSet(Object obj)
        {
            _resultAction(new UIInstruction { Type = UIType.CalibrationStopQuery, Result = false });
        }

    }
}
