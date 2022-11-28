using CoreServices.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Input;

namespace ViewModels.Calibration
{
    public class CalibrationRepeatQueryViewModel:ViewModelBase
    {
        private string _repeatText;
        public ICommand YesCommand { get; set; }
        public ICommand NoCommand { get; set; }
        public string RepeatText { get=>_repeatText; set { _repeatText = value;OnPropertyChanged(nameof(RepeatText)); } }
        private Action<UIInstruction> _resultAction;

        public CalibrationRepeatQueryViewModel(Action<UIInstruction> resultAction)
        {
            YesCommand = new RelayCommand(RepeatCalibrationSet);
            NoCommand = new RelayCommand(KeepCalibrationSet);
            _resultAction = resultAction;
        }

        private void RepeatCalibrationSet(Object obj)
        {
            _resultAction(new UIInstruction { Type = UIType.CalibrationRepeatQuery, Result = true });
        }

        private void KeepCalibrationSet(Object obj)
        {
            _resultAction(new UIInstruction { Type = UIType.CalibrationRepeatQuery, Result = false });
        }
    }
}
