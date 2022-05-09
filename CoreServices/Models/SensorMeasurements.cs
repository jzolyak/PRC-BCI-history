using System;
using System.Collections.Generic;
using System.Text;

namespace CoreServices.Models
{
    public class SensorMeasurements
    {
        public string Sensor { get; set; }
        public double Impedance { get; set; }
        public double Level { get; set; }
        public double Noise { get; set; }
    }

    public struct SensorEvents
    {
        public int noiseLevelOutOfRange;
        public int noiseLevelAdjust;
        public int noiseLevelGood;
        public int baselineLevelOutOfRange;
        public int baselineLevelAdjust;
        public int baselineLevelGood;
        public int impedancOutOfRange;
        public int impedanceAdjust;
        public int impedanceGood;
        public int impedanceBad;
    }
}
