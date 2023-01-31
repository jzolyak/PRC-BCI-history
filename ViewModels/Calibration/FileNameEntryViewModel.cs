using CoreServices.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Windows.Input;

namespace ViewModels.Calibration
{
    public class FileNameEntryViewModel : StartCalibrationViewModel
    {
        private string _fileName;
        private CalibrationFileMetadata _calibrationFileMetadata;
        public string FileName { get { return _fileName; } set { _fileName = value; OnPropertyChanged(nameof(FileName)); } }
        public ICommand OkCommand { get; set; }
        public ICommand ClearCommand { get; set; }
        public FileNameEntryViewModel(Action startCalibrationAction,Action closePopup,CalibrationFileMetadata calibrationFileMetadata) : base(startCalibrationAction,closePopup)
        {
            _calibrationFileMetadata = calibrationFileMetadata;
            OkCommand = new RelayCommand(CheckFileName);
            ClearCommand = new RelayCommand((x) => { FileName = String.Empty; });
        }
        private void CheckFileName(object obj)
        {
            if(string.IsNullOrEmpty(FileName))
            {
                throw new Exception("Please enter valid file name");
            }
            if(_calibrationFileMetadata != null)
            {
                var existingFile = _calibrationFileMetadata.UserGivenName.ToLower().Equals(FileName.ToLower());
                if(existingFile)
                {
                    throw new Exception("File name already exists. Please provide different file name");
                }
            }
            //All checks passed
            StartCalibration(new object());
        }

    }
}
