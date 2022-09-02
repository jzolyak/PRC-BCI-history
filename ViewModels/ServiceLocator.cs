using CoreServices.Calibration;
using CoreServices.Connectivity;
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
    }
}
