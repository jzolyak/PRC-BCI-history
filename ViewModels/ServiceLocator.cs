using CoreServices.Calibration;
using CoreServices.Connectivity;
using CoreServices.Presets;
using CoreServices.Prediction;
using CoreServices.Headset;
using CoreServices.Common;

namespace ViewModels
{
    public class ServiceLocator
    {
        private static HeadSetConnectivityService? _headSetConnectivityService;
        public static HeadSetConnectivityService HeadSetConnectivityService
        {
            get
            {
                if (_headSetConnectivityService == null)
                    _headSetConnectivityService = new HeadSetConnectivityService();
                return _headSetConnectivityService;
            }
        }

        private static CalibrationService? _calibrationService;
        public static CalibrationService CalibrationService
        {
            get
            {
                if (_calibrationService == null)
                    _calibrationService = new CalibrationService();
                return _calibrationService;
            }
        }

        private static PredictionService? _predictionService;
        public static PredictionService PredictionService
        {
            get
            {
                if (_predictionService == null)
                    _predictionService = new PredictionService();
                return _predictionService;
            }
        }
        
        private static UserSettingsService? _userSettingsService;
        public static UserSettingsService UserSettingsService
        {
            get
            {
                if (_userSettingsService == null)
                    _userSettingsService = new UserSettingsService();
                return _userSettingsService;
            }
        }


        private static PresetsService? _presetsService;
        public static PresetsService PresetsService
        {
            get
            {
                if (_presetsService == null)
                    _presetsService =PresetsService.Instance;
                return _presetsService;
            }
        }

        private static HeadsetService? _headsetService;
        public static HeadsetService HeadsetService
        {
            get
            {
                if (_headsetService == null)
                    _headsetService = new HeadsetService();
                return _headsetService;
            }
        }

    }
}
