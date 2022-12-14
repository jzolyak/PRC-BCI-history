using CoreServices.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Input;

namespace ViewModels.Calibration
{
    public class CalibrationBeginQueryViewModel:ViewModelBase
    {
        private string _text;
        public ICommand StartCommand { get; set; }
        public ICommand CancelCommand { get; set; }
        public string Text { get => _text; set { _text = value; OnPropertyChanged(nameof(Text)); } }
        private Action<UIInstruction> _resultAction;

        public CalibrationBeginQueryViewModel(Action<UIInstruction> resultAction)
        {
            StartCommand = new RelayCommand(StartCalibrationSet);
            CancelCommand = new RelayCommand(CacnelCalibrationSet);
            _resultAction = resultAction;
            Text = "Are you ready to begin Calibration Set 1?";
        }

        public void UpdateText(CalibrationFileMetadata calibrationFileMetadata)
        {
            if (calibrationFileMetadata == null)
            {
                return;
            }
            Text = "Are you ready to begin Calibration Set " + calibrationFileMetadata.CurrentSet + " ?";
        }

        private void StartCalibrationSet(Object obj)
        {
            _resultAction(new UIInstruction { Type = UIType.CalibrationBeginQuery, Result = true });
        }

        private void CacnelCalibrationSet(Object obj)
        {
            _resultAction(new UIInstruction { Type = UIType.CalibrationBeginQuery, Result = false });
        }
    }
}
