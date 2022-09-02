using CoreServices.Models;
using System;
using System.Collections.Generic;
using System.Text;
using System.Windows.Input;

namespace ViewModels.Calibration
{
    public class DeleteConfirmationViewModel : StartCalibrationViewModel
    { 
        public CalibrationFileMetadata CalibrationFileMetadata { get; set; }
        public DeleteConfirmationViewModel(Action startCalibrationAction,Action closePopup,CalibrationFileMetadata calibrationFileMetadata) : base(startCalibrationAction, closePopup)
        {
            CalibrationFileMetadata = calibrationFileMetadata;
        }

    }
}
