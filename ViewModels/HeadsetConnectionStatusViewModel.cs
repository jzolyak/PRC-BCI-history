using System.Collections.Generic;
using CoreServices.Models;

namespace ViewModels
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

        public HeadsetConnectionStatusViewModel()
        {
            _sensors = new Dictionary<string, SensorStatusViewModel>();
        }
    }
}
