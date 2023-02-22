using CoreServices.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Input;

namespace ViewModels.Calibration
{
    public class CalibrationCompleteViewModel: ViewModelBase
    {
        Action<UIInstruction> _resultAction;
        public ICommand OkCommand { get; set; }


        public CalibrationCompleteViewModel(Action<UIInstruction> resultAction)
        {
            _resultAction=resultAction;
            OkCommand=  new RelayCommand(Ok);
        }
        private void Ok(Object obj)
        {
            _resultAction(new UIInstruction { Type = UIType.CalibrationComplete, Result = true });
        }

    }
}
