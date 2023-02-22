using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Input;

namespace ViewModels.Calibration
{
    public class CalibrationInstructionsViewModel: StartCalibrationViewModel
    {
      
        
        public CalibrationInstructionsViewModel(Action startCalibrationAction, Action closePopup):base(startCalibrationAction, closePopup)
        {
          
            
        }
    }
}
