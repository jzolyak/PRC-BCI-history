using System;
using System.Collections.Generic;
using System.Drawing;
using System.Text;

namespace ViewModels
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

        private string? _sensorText;
        public string? SensorText
        {
            get { return _sensorText; }
            set { _sensorText = value; OnPropertyChanged(nameof(SensorText)); }
        }
    }
}
