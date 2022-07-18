using CoreServices.Models;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Text;

namespace ViewModels.Connectivity
{
    public class SensorStatusViewModel:ViewModelBase
    {
        private Color _sensorBGColor;
        public Color SensorBGColor
        {
            get { return _sensorBGColor; }
            set { _sensorBGColor = value;OnPropertyChanged(nameof(SensorBGColor)); }
        }

        private string? _sensorName;
        public string? SensorName
        {
            get { return _sensorName; }
            set { _sensorName = value; OnPropertyChanged(nameof(SensorName)); }
        }

        private string? _sensorStatus;
        public string? SensorStatus
        {
            get { return _sensorStatus; }
            set { _sensorStatus = value; OnPropertyChanged(nameof(SensorStatus)); }
        }

        private string? _sensorText;
        public string? SensorText
        {
            get { return _sensorText; }
            set { _sensorText = value; OnPropertyChanged(nameof(SensorText)); }
        }

        public SensorStatusViewModel(string _sensorName)
        {
            SensorName = _sensorName;
            SensorText = "Twist";
            SensorBGColor = Color.Red;
            SensorStatus = Constants.Sensor_Status_Red;
        }
    }
}
