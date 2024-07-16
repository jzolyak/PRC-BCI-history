using CoreServices;
using CoreServices.Common;
using CoreServices.Connectivity;
using CoreServices.Models;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Drawing;
using System.Text;
using System.Windows.Input;

namespace ViewModels.Connectivity
{
    public class ConnectivityViewModel : ExitToMainMenuViewModel
    {
        private const string portKey= "HeadSetPort";
        public HeadsetConnectionStatusViewModel? HeadsetConnectionStatusViewModel { get; set; }
        public ObservableCollection<SensorMeasurementViewModel>? MeasurementList { get; set; }
        private List<SensorMeasurementViewModel> _measurements;
        private UserSettingsService _userSettingsService;
        private HeadSetConnectivityService _headSetConnectivityService;
        private string _recommandation;
        private string _port;
        public string Recommandation { get { return _recommandation; } set { _recommandation = value; OnPropertyChanged(nameof(Recommandation)); } }
        public bool IsInTablet { get; set; }

        public ICommand StartCommand { get; set; }
        public ICommand ContinueCommand { get; set; }
        public ICommand ResetCommand { get; set; }

        public ICommand DisconnectCommand { get; set; }
        private bool _enableStart;
        private bool _enableMiscCommands;
        private bool _enableExit;
        

        public bool EnableStart { get { return _enableStart; } set { _enableStart = value; OnPropertyChanged(nameof(EnableStart)); } }
        public bool EnableMiscCommands { get { return _enableMiscCommands; } set { _enableMiscCommands = value; OnPropertyChanged(nameof(EnableMiscCommands)); } }
        public bool EnableExit { get { return _enableExit; } set { _enableExit = value; OnPropertyChanged(nameof(EnableExit)); } }

        public string Port { get => _port; set { _port = value; ChangePort(); OnPropertyChanged(nameof(Port)); } }
       public ObservableCollection<string> Ports { get; set; }

        public ConnectivityViewModel(Action exitAction,Action<bool> showorHideMainWindow) : base(exitAction, showorHideMainWindow)
        {
            StartCommand = new RelayCommand(StartConnection);
            EnableStart = true;
            EnableMiscCommands = false;
            EnableExit = true;
            _headSetConnectivityService = ServiceLocator.HeadSetConnectivityService;
            _headSetConnectivityService.RaiseMeasurementsChange += HeadSetConnectivityService_RaiseMeasurementsChange;
            _headSetConnectivityService.RaiseRecommendationChange += HeadSetConnectivityService_RaiseRecommendationChange;
            _headSetConnectivityService.RaiseSensorStatusColorChange += _headSetConnectivityService_RaiseSensorStatusColorChange;
            var initialMeasurements = _headSetConnectivityService.GetCurrentSensorMeasurements();
            _measurements = new List<SensorMeasurementViewModel>();
            if (initialMeasurements != null && initialMeasurements.Count > 0)
            {
                initialMeasurements.ForEach(measure => { if (measure != null) _measurements.Add(new SensorMeasurementViewModel(measure)); });
            }
            MeasurementList = new ObservableCollection<SensorMeasurementViewModel>(_measurements);
            HeadsetConnectionStatusViewModel = new HeadsetConnectionStatusViewModel();
            ContinueCommand = new RelayCommand(ContinueConnection);
            ResetCommand = new RelayCommand(Reset);
            Recommandation = "Recommended action to take next appears here...";
            _userSettingsService = ServiceLocator.UserSettingsService;
            _port = _userSettingsService.GetSettingsValue(portKey);
            if(Port == null)
            {
                _port = "COM8";
            }
            OnPropertyChanged(nameof(Port));
            DisconnectCommand = new RelayCommand(Disconnect);
            Ports = new ObservableCollection<string>();
            GetAvailablePorts();
            _headSetConnectivityService.UpdateHeadsetPort(Port);
        }

        private void GetAvailablePorts()
        {
            try
            {
                AvailablePorts ports = new AvailablePorts();
                ports.GetBluetoothCOMPort();
                ports.COMPorts.ForEach((port) => { Ports.Add(port.SerialPort.ToUpper()); });
            }
            catch (Exception ex)
            {
                Logger.Error(this, ex.Message);
            }
            if(!Ports.Contains(_port))
            {
                Ports.Add(_port);
                _port = Ports[0];
            }
            
        }

        private void Disconnect(object obj)
        {
            _headSetConnectivityService.Exit();
            base.OnExit(obj);
        }

        private void ChangePort()
        {
            _userSettingsService.SaveUserSettings(portKey,_port);
            _headSetConnectivityService.UpdateHeadsetPort(_port);
        }

        private void Reset(object obj)
        {
           _headSetConnectivityService?.Reset();
        }

        private void _headSetConnectivityService_RaiseSensorStatusColorChange(object sender, EventArgs<int[]> e)
        {
            var statusColor = e.Value;
            if (statusColor == null || statusColor.Length < 8 || HeadsetConnectionStatusViewModel == null) return;
            // Interpret the action to take based on the value given in the statusColor.
            if (statusColor[0] >= 5)        // Red - Twist sensor in hair, push down on sensor for 2 sec
            {
                HeadsetConnectionStatusViewModel.FZ_Sensor.SensorBGColor = Color.Red;
                HeadsetConnectionStatusViewModel.FZ_Sensor.SensorStatus = Constants.Sensor_Status_Red;
                HeadsetConnectionStatusViewModel.FZ_Sensor.SensorText = "Twist";
            }
            else if (statusColor[0] >= 1)       // Yellow - Push down on sensor for 2 sec, no twisting
            {
                HeadsetConnectionStatusViewModel.FZ_Sensor.SensorBGColor = Color.Yellow;
                HeadsetConnectionStatusViewModel.FZ_Sensor.SensorStatus = Constants.Sensor_Status_Yellow;
                HeadsetConnectionStatusViewModel.FZ_Sensor.SensorText = "Push";
            }
            else
            {

                HeadsetConnectionStatusViewModel.FZ_Sensor.SensorBGColor = Color.Green;
                HeadsetConnectionStatusViewModel.FZ_Sensor.SensorStatus = Constants.Sensor_Status_Green;
                HeadsetConnectionStatusViewModel.FZ_Sensor.SensorText = "Good";   // Good measurement, no adjustment needed
            }

            if (statusColor[1] >= 5)        // Red
            {
                HeadsetConnectionStatusViewModel.P3_Sensor.SensorBGColor = Color.Red;
                HeadsetConnectionStatusViewModel.P3_Sensor.SensorStatus = Constants.Sensor_Status_Red;
                HeadsetConnectionStatusViewModel.P3_Sensor.SensorText = "Twist";
            }
            else if (statusColor[1] >= 1)       // Yellow
            {
                HeadsetConnectionStatusViewModel.P3_Sensor.SensorBGColor = Color.Yellow;
                HeadsetConnectionStatusViewModel.P3_Sensor.SensorStatus = Constants.Sensor_Status_Yellow;
                HeadsetConnectionStatusViewModel.P3_Sensor.SensorText = "Push";
            }
            else
            {
                HeadsetConnectionStatusViewModel.P3_Sensor.SensorBGColor = Color.Green;
                HeadsetConnectionStatusViewModel.P3_Sensor.SensorStatus = Constants.Sensor_Status_Green;
                HeadsetConnectionStatusViewModel.P3_Sensor.SensorText = "Good";
            }

            if (statusColor[2] >= 5)        // Red
            {
                HeadsetConnectionStatusViewModel.Pz_Sensor.SensorBGColor = Color.Red;
                HeadsetConnectionStatusViewModel.Pz_Sensor.SensorStatus = Constants.Sensor_Status_Green;
                HeadsetConnectionStatusViewModel.Pz_Sensor.SensorText = "Twist";
            }
            else if (statusColor[2] >= 1)       // Yellow
            {
                HeadsetConnectionStatusViewModel.Pz_Sensor.SensorBGColor = Color.Yellow;
                HeadsetConnectionStatusViewModel.Pz_Sensor.SensorStatus = Constants.Sensor_Status_Yellow;
                HeadsetConnectionStatusViewModel.Pz_Sensor.SensorText = "Push";
            }
            else
            {
                HeadsetConnectionStatusViewModel.Pz_Sensor.SensorBGColor = Color.Green;
                HeadsetConnectionStatusViewModel.Pz_Sensor.SensorStatus = Constants.Sensor_Status_Green;
                HeadsetConnectionStatusViewModel.Pz_Sensor.SensorText = "Good";
            }

            if (statusColor[3] >= 5)        // Red
            {
                HeadsetConnectionStatusViewModel.P4_Sensor.SensorBGColor = Color.Red;
                HeadsetConnectionStatusViewModel.P4_Sensor.SensorStatus = Constants.Sensor_Status_Red;
                HeadsetConnectionStatusViewModel.P4_Sensor.SensorText = "Twist";
            }
            else if (statusColor[3] >= 1)       // Yellow
            {
                HeadsetConnectionStatusViewModel.P4_Sensor.SensorBGColor = Color.Yellow;
                HeadsetConnectionStatusViewModel.P4_Sensor.SensorStatus = Constants.Sensor_Status_Yellow;
                HeadsetConnectionStatusViewModel.P4_Sensor.SensorText = "Push";
            }
            else
            {
                HeadsetConnectionStatusViewModel.P4_Sensor.SensorBGColor = Color.Green;
                HeadsetConnectionStatusViewModel.P4_Sensor.SensorStatus = Constants.Sensor_Status_Green;
                HeadsetConnectionStatusViewModel.P4_Sensor.SensorText = "Good";
            }


            if (statusColor[4] >= 5)        // Red
            {
                HeadsetConnectionStatusViewModel.PO7_Sensor.SensorBGColor = Color.Red;
                HeadsetConnectionStatusViewModel.PO7_Sensor.SensorStatus = Constants.Sensor_Status_Red;
                HeadsetConnectionStatusViewModel.PO7_Sensor.SensorText = "Twist";
            }
            else if (statusColor[4] >= 1)       // Yellow
            {
                HeadsetConnectionStatusViewModel.PO7_Sensor.SensorBGColor = Color.Yellow;
                HeadsetConnectionStatusViewModel.PO7_Sensor.SensorStatus = Constants.Sensor_Status_Yellow;
                HeadsetConnectionStatusViewModel.PO7_Sensor.SensorText = "Push";
            }
            else
            {
                HeadsetConnectionStatusViewModel.PO7_Sensor.SensorBGColor = Color.Green;
                HeadsetConnectionStatusViewModel.PO7_Sensor.SensorStatus = Constants.Sensor_Status_Green;
                HeadsetConnectionStatusViewModel.PO7_Sensor.SensorText = "Good";
            }


            if (statusColor[5] >= 5)        // Red
            {
                HeadsetConnectionStatusViewModel.Oz_Sensor.SensorBGColor = Color.Red;
                HeadsetConnectionStatusViewModel.Oz_Sensor.SensorStatus = Constants.Sensor_Status_Red;
                HeadsetConnectionStatusViewModel.Oz_Sensor.SensorText = "Twist";
            }
            else if (statusColor[5] >= 1)       // Yellow
            {
                HeadsetConnectionStatusViewModel.Oz_Sensor.SensorBGColor = Color.Yellow;
                HeadsetConnectionStatusViewModel.Oz_Sensor.SensorStatus = Constants.Sensor_Status_Yellow;
                HeadsetConnectionStatusViewModel.Oz_Sensor.SensorText = "Push";
            }
            else
            {
                HeadsetConnectionStatusViewModel.Oz_Sensor.SensorBGColor = Color.Green;
                HeadsetConnectionStatusViewModel.Oz_Sensor.SensorStatus = Constants.Sensor_Status_Green;
                HeadsetConnectionStatusViewModel.Oz_Sensor.SensorText = "Good";
            }

            if (statusColor[6] >= 5)        // Red
            {
                HeadsetConnectionStatusViewModel.PO8_Sensor.SensorBGColor = Color.Red;
                HeadsetConnectionStatusViewModel.PO8_Sensor.SensorStatus = Constants.Sensor_Status_Red;
                HeadsetConnectionStatusViewModel.PO8_Sensor.SensorText = "Twist";
            }
            else if (statusColor[6] >= 1)       // Yellow
            {
                HeadsetConnectionStatusViewModel.PO8_Sensor.SensorBGColor = Color.Yellow;
                HeadsetConnectionStatusViewModel.PO8_Sensor.SensorStatus = Constants.Sensor_Status_Yellow;
                HeadsetConnectionStatusViewModel.PO8_Sensor.SensorText = "Push";
            }
            else
            {
                HeadsetConnectionStatusViewModel.PO8_Sensor.SensorBGColor = Color.Green;
                HeadsetConnectionStatusViewModel.PO8_Sensor.SensorStatus = Constants.Sensor_Status_Green;
                HeadsetConnectionStatusViewModel.PO8_Sensor.SensorText = "Good";
            }

            if (statusColor[7] >= 5)        // Red
            {
                HeadsetConnectionStatusViewModel.Ear_Sensor.SensorBGColor = Color.Red;
                HeadsetConnectionStatusViewModel.Ear_Sensor.SensorStatus = Constants.Sensor_Status_Red;
                HeadsetConnectionStatusViewModel.Ear_Sensor.SensorText = "Twist";
            }
            else if (statusColor[7] >= 1)       // Yellow
            {
                HeadsetConnectionStatusViewModel.Ear_Sensor.SensorBGColor = Color.Yellow;
                HeadsetConnectionStatusViewModel.Ear_Sensor.SensorStatus = Constants.Sensor_Status_Yellow;
                HeadsetConnectionStatusViewModel.Ear_Sensor.SensorText = "Push";
            }
            else
            {
                HeadsetConnectionStatusViewModel.Ear_Sensor.SensorBGColor = Color.Green;
                HeadsetConnectionStatusViewModel.Ear_Sensor.SensorStatus = Constants.Sensor_Status_Green;
                HeadsetConnectionStatusViewModel.Ear_Sensor.SensorText = "Good";
            }


        }

        private void HeadSetConnectivityService_RaiseRecommendationChange(object sender, EventArgs<string> e)
        {
            if(!string.IsNullOrEmpty(e.Value))
            {
                if(e.Value.Contains("failed"))
                {
                    HeadsetConnectionStatusViewModel.HeadsetConnectionStatus = Constants.NotConnected; ;
                }
                else if(e.Value.StartsWith("Connection to COM port passed"))
                {
                    HeadsetConnectionStatusViewModel.HeadsetConnectionStatus = Constants.Connected;
                }
            }
            Recommandation = e.Value;
        }

        private void HeadSetConnectivityService_RaiseMeasurementsChange(object sender, EventArgs<List<SensorMeasurements>> e)
        {
            //MeasurementList
            if (e == null || e.Value == null || e.Value.Count ==0) return;

            e.Value.ForEach(m => {
                var measurement = _measurements.Find(x=>x.Sensor.Equals(m.Sensor));
                if (measurement != null)
                {
                    measurement.Impedance = Math.Round(m.Impedance,4);
                    measurement.Noise = Math.Round(m.Noise,4);
                    measurement.Level = Math.Round(m.Level,4);
                }
            });
        }

        private async void StartConnection(object obj)
        {
            try
            {
                if (_headSetConnectivityService == null) { Logger.Error(this, "head set connectivity service is null"); return; }
                EnableStart = false;
                EnableMiscCommands = false;
                EnableExit = false;
                Logger.Info(this, "Stating connection");
                await _headSetConnectivityService.ConnectivityMode();
                EnableMiscCommands = true ;
                EnableExit = true;

            }
            catch (Exception ex)
            {
                Logger.Error(this, ex.Message);
            }
        }

        private void ContinueConnection(object obj)
        {
            EnableMiscCommands = false;
            EnableExit = false;
            _headSetConnectivityService.Continue();
            EnableMiscCommands = true;
            EnableExit = true;
        }
    }
}
