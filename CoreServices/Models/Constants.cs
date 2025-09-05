    using System;
using System.Collections.Generic;
using System.Text;

namespace CoreServices.Models
{
    public class Constants
    {
        public static readonly string FZ_Sensor = "Fz";
        public static readonly string P3_Sensor = "P3";
        public static readonly string Pz_Sensor = "Pz";
        public static readonly string P4_Sensor = "P4";
        public static readonly string PO7_Sensor = "PO7";
        public static readonly string Oz_Sensor = "Oz";
        public static readonly string PO8_Sensor ="PO8";
        public static readonly string Ear_Sensor = "Ear";

        public static  string Connectivity = "Connectivity";
        public static readonly string Calibration = "Calibration";
        public static readonly string Validation = "Validation";
        public static readonly string Prediction = "Prediction";
        public static readonly string Flash = "Flash";
        public static readonly string Misc = "Misc";
        public static readonly string System = "System";
        public static readonly string Logging = "Logging";
        public static readonly string ConnectivitySettings = "ConnectivitySettings";
        public static readonly string CalibrationSettings = "CalibrationSettings";
        public static readonly string ValidationSettings = "ValidationSettings";
        public static readonly string FlashSettings = "FlashSettings";
        public static readonly string PredictionSettings = "PredictionSettings";
        public static readonly string MiscSettings = "MiscSettings";
        public static readonly string SystemSettings = "SystemSettings";
        public static readonly string LoggingSettings = "LoggingSettings";
        public static readonly string Help = "Help";
        public static readonly string Settings = "Settings";
        public static readonly string FlashStimulus = "FlashStimulus";
        

        public const string Connected = "Connected";
        public const string NotConnected = "NotConnected";

        public const string Sensor_Status_Green = "Green";
        public const string Sensor_Status_Yellow = "Yellow";
        public const string Sensor_Status_Red = "Red";
        public const string DemoVideo_Preference_Key = "ShowDemoVideo";



    }

    public enum UIType
    {
        CalibrationStopQuery,
        CalibrationRepeatQuery,
        CalibrationBeginQuery,
        CountDownWindow,
        CalibrationComplete
    }
}
