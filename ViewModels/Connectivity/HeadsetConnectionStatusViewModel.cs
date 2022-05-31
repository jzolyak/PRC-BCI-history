using System.Collections.Generic;
using CoreServices.Models;

namespace ViewModels.Connectivity
{
    public class HeadsetConnectionStatusViewModel:ViewModelBase
    {
        private readonly Dictionary<string, SensorStatusViewModel>? _sensors;

        public SensorStatusViewModel? FZ_Sensor
        {
            get { return _sensors?.GetValueOrDefault(Constants.FZ_Sensor); }
        }
        public SensorStatusViewModel? P3_Sensor
        {
            get { return _sensors?.GetValueOrDefault(Constants.P3_Sensor); }
        }
        public SensorStatusViewModel? Pz_Sensor
        {
            get { return _sensors?.GetValueOrDefault(Constants.Pz_Sensor); }
        }
        public SensorStatusViewModel? P4_Sensor
        {
            get { return _sensors?.GetValueOrDefault(Constants.P4_Sensor); }
        }
        public SensorStatusViewModel? Oz_Sensor
        {
            get { return _sensors?.GetValueOrDefault(Constants.Oz_Sensor); }
        }
        public SensorStatusViewModel? PO7_Sensor
        {
            get { return _sensors?.GetValueOrDefault(Constants.PO7_Sensor); }
        }
        public SensorStatusViewModel? PO8_Sensor
        {
            get { return _sensors?.GetValueOrDefault(Constants.PO8_Sensor); }
        }
        public SensorStatusViewModel? Ear_Sensor
        {
            get { return _sensors?.GetValueOrDefault(Constants.Ear_Sensor); }
        }

        private string _headsetConnectionStatus;

        public string HeadsetConnectionStatus { get { return _headsetConnectionStatus; } set { _headsetConnectionStatus = value; OnPropertyChanged(nameof(HeadsetConnectionStatus)); } }

        public HeadsetConnectionStatusViewModel()
        {
            _headsetConnectionStatus = Constants.NotConnected;
            _sensors = new Dictionary<string, SensorStatusViewModel>();
            _sensors.Add(Constants.FZ_Sensor, new SensorStatusViewModel(Constants.FZ_Sensor));
            _sensors.Add(Constants.P3_Sensor, new SensorStatusViewModel(Constants.P3_Sensor));
            _sensors.Add(Constants.Pz_Sensor, new SensorStatusViewModel(Constants.Pz_Sensor));
            _sensors.Add(Constants.P4_Sensor, new SensorStatusViewModel(Constants.P4_Sensor));
            _sensors.Add(Constants.Oz_Sensor, new SensorStatusViewModel(Constants.Oz_Sensor));
            _sensors.Add(Constants.PO7_Sensor, new SensorStatusViewModel(Constants.PO7_Sensor));
            _sensors.Add(Constants.PO8_Sensor, new SensorStatusViewModel(Constants.PO8_Sensor));
            _sensors.Add(Constants.Ear_Sensor, new SensorStatusViewModel(Constants.Ear_Sensor));

        }
    }
}
