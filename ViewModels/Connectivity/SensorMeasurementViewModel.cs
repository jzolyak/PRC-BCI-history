using CoreServices.Models;
using System;
using System.Collections.Generic;
using System.Text;

namespace ViewModels.Connectivity
{
    public class SensorMeasurementViewModel:ViewModelBase
    {
        private string _sensor;
        private double _impedance;
        private double _level;
        private double _noise;
        public string Sensor { get { return _sensor; } set { _sensor = value; OnPropertyChanged(nameof(Sensor)); } }
        public double Level { get { return _level; } set { _level = value; OnPropertyChanged(nameof(Level)); } }
        public double Noise { get { return _noise; } set { _noise = value; OnPropertyChanged(nameof(Noise)); } }
        public double Impedance { get { return _impedance; } set { _impedance = value; OnPropertyChanged(nameof(Impedance)); } }    

        public SensorMeasurementViewModel(SensorMeasurements sensorMeasurements)
        {
            Sensor = sensorMeasurements.Sensor;
            Level = sensorMeasurements.Level;
            Noise = sensorMeasurements.Noise;
            Impedance = sensorMeasurements.Impedance;
        }
    }
}
