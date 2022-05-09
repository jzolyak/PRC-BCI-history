using CoreServices.Calibration;

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
    }
}
