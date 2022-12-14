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
        private string _text;
        public string Text { get { return _text; } set { _text = value; OnPropertyChanged(nameof(Text)); } }

        public CalibrationStopQueryViewModel(Action<UIInstruction> resultAction)
        {
            KeepCommand = new RelayCommand(KeepCalibrationSet);
            DiscardCommand = new RelayCommand(DiscardCalibrationSet);
            Text = "Do you want to Keep or Discard data collected in Calibration Set 1 of 5 ?";
            _resultAction = resultAction;
        }

        public void UpdateText(CalibrationFileMetadata calibrationFileMetadata)
        {
           if(calibrationFileMetadata == null)
            {
                return;
            }
           Text = "Do you want to Keep or Discard data collected in Calibration Set " +calibrationFileMetadata.CurrentSet +" of " +calibrationFileMetadata.NumofSets+ " ?";
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
