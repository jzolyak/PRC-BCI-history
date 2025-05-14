using CoreServices.Calibration;
using CoreServices.Common;
using CoreServices.Models;
using CoreServices.Prediction;
using CoreServices.Stimulus;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Input;
using ViewModels.Calibration;
using ViewModels.Help;

namespace ViewModels.FlashStimulus
{
    public class FlashStimulusViewModel:ViewModelBase
    {
        private double _top;
        private double _left;
        private double _height;
        private double _width;
        private ObservableCollection<ButtonElement> _buttonCollection;
        private CalibrationService _calibrationService;
        private PredictionService _predictionService;
        private string _mode;
        private Action _exitAction;
        private UIInstruction _uiInstruction;
        private object? _popupContentViewModel;
        private bool _showPopup;
        //private bool _showFloatingPopup;
        private bool _showFloatingPopup = false;
        private WindowTopMost _topMost;
        private CalibrationStopQueryViewModel _stopQueryViewModel;
        private CalibrationBeginQueryViewModel _beginQueryViewModel;
        private CalibrationCompleteViewModel _completeViewModel;
        private CalibrationRepeatQueryViewModel _repeatQueryViewModel;
        private CountDownViewModel _countDownViewModel;
        private WindowSizeProperties _windowSizeProp;
        private CoeffFileGenerationProgressViewModel _coeffFileGenerationProgressViewModel;


        public double Top { get { return _top; } set { _top = value; OnPropertyChanged(nameof(Top)); } }
        public double Left { get { return _left; } set { _left = value; OnPropertyChanged(nameof(Left)); } }
        public double Height { get { return _height; } set { _height = value; OnPropertyChanged(nameof(Height)); } }
        public double Width { get { return _width; } set { _width = value; OnPropertyChanged(nameof(Width)); } }
        public WindowSizeProperties WindowSizeProp { get => _windowSizeProp; set { _windowSizeProp = value; OnPropertyChanged(nameof(WindowSizeProp)); } }
        public ObservableCollection<ButtonElement> ButtonCollection { get { return _buttonCollection; } set { _buttonCollection = value; OnPropertyChanged(nameof(ButtonCollection)); } }
        public bool ShowPopup { get => _showPopup; set { _showPopup = value; OnPropertyChanged(nameof(ShowPopup)); } }
        public bool ShowFloatingPopup { get => _showFloatingPopup; set { _showFloatingPopup = value; OnPropertyChanged(nameof(ShowFloatingPopup)); } }
        public object? PopupContentViewModel { get => _popupContentViewModel; set { _popupContentViewModel = value; OnPropertyChanged(nameof(PopupContentViewModel)); } }
        public WindowTopMost Topmost { get => _topMost; set { _topMost = value; OnPropertyChanged(nameof(Topmost)); } }
        
        public ICommand ExitCommand { get; set; }
        public FlashStimulusViewModel(string mode,Action exitAction)
        {
            _topMost = new WindowTopMost(); 
            _calibrationService = ServiceLocator.CalibrationService;
            _predictionService= ServiceLocator.PredictionService;
            _buttonCollection = new ObservableCollection<ButtonElement>();
            _calibrationService.RaiseScreenOverLayChange += _calibrationService_RaiseScreenOverLayChange;
            _calibrationService.RaiseUIRequestChange += _calibrationService_RaiseUIRequestChange;
            _calibrationService.RaiseCoeffInProgress += _calibrationService_RaiseCoeffInProgress;
            _calibrationService.RaiseCoeffCompleted += _calibrationService_RaiseCoeffCompleted;
            _predictionService.RaiseScreenOverLayChange += _predictionService_RaiseScreenOverLayChange;
            _stopQueryViewModel = new CalibrationStopQueryViewModel(NotificationFromUI);
            _beginQueryViewModel = new CalibrationBeginQueryViewModel(NotificationFromUI);
            _completeViewModel = new CalibrationCompleteViewModel(NotificationFromUI);
            _repeatQueryViewModel = new CalibrationRepeatQueryViewModel(NotificationFromUI);
            _countDownViewModel = new CountDownViewModel(ClosePopup, NotificationFromUI);
            _coeffFileGenerationProgressViewModel = new CoeffFileGenerationProgressViewModel();
            ExitCommand = new RelayCommand(ExitToMainMenu);
            WindowSizeProp = null;
            _mode = mode;
            _exitAction = exitAction;
        }

        private void _calibrationService_RaiseCoeffCompleted(object? sender, EventArgs e)
        {
            ClosePopup();
        }
        private void _calibrationService_RaiseCoeffInProgress(object? sender, EventArgs e)
        {
            PopupContentViewModel = _coeffFileGenerationProgressViewModel;
            OpenPopup();
        }

        private void _calibrationService_RaiseUIRequestChange(object? sender, CoreServices.Models.EventArgs<CoreServices.Models.UIInstruction> e)
        {
            if (e == null || e.Value == null) return;
            _uiInstruction = e.Value;
            CalibrationFileMetadata? calibrationFileMetadata = _uiInstruction.Param != null ? (CalibrationFileMetadata)_uiInstruction.Param : null;
           switch(_uiInstruction.Type)
            {
                case UIType.CalibrationStopQuery: PopupContentViewModel = _stopQueryViewModel; _stopQueryViewModel.UpdateText(calibrationFileMetadata); OpenPopup(); break;
                case UIType.CalibrationBeginQuery: PopupContentViewModel = _beginQueryViewModel; _beginQueryViewModel.UpdateText(calibrationFileMetadata); OpenPopup(); break;
                case UIType.CalibrationRepeatQuery: PopupContentViewModel = _repeatQueryViewModel; _repeatQueryViewModel.UpdateText(calibrationFileMetadata); ; OpenPopup(); break;
                case UIType.CalibrationComplete: PopupContentViewModel = _completeViewModel; OpenPopup(); break;
                case UIType.CountDownWindow: PopupContentViewModel = _countDownViewModel; _countDownViewModel.UpdateTitle(calibrationFileMetadata); _countDownViewModel.Start(); OpenPopup(); break;
            }
        }

        private void _calibrationService_RaiseScreenOverLayChange(object? sender, CoreServices.Models.EventArgs<ScreenOverlay> e)
        {
            if (!string.IsNullOrEmpty(_mode) && _mode.ToLower().Equals("prediction"))
            {
                return;
            }
            Topmost = new WindowTopMost();
            ButtonCollection = null;
            if (e==null || e.Value == null) return;  
            var screenOverlay = e.Value;
            if (screenOverlay == null || screenOverlay.ButtonElementList == null) return;
            screenOverlay.ButtonElementList.Add(GetExitToMainScreen(screenOverlay.ButtonElementList[0]));
            ButtonCollection = new ObservableCollection<ButtonElement>(screenOverlay.ButtonElementList);
            this.Height = screenOverlay.Height+150;
            this.Width = screenOverlay.Width;
            this.Top = screenOverlay.Top;
            this.Left = screenOverlay.Left;
            WindowSizeProp = new WindowSizeProperties() { Height = screenOverlay.Height, Left = screenOverlay.Left, Top = screenOverlay.Top, Width = screenOverlay.Width };
        }

        private void _predictionService_RaiseScreenOverLayChange(object? sender, CoreServices.Models.EventArgs<ScreenOverlay> e)
        {
            if (!string.IsNullOrEmpty(_mode) && _mode.ToLower().Equals("calibration"))
            {
                return;
            }
            ShowFloatingPopup =true;
            //ShowFloatingPopup = true; 
            Topmost = new WindowTopMost();
            ButtonCollection = null;
            if (e == null || e.Value == null) return;
            var screenOverlay = e.Value;
            if (screenOverlay == null || screenOverlay.ButtonElementList == null) return;
            screenOverlay.ButtonElementList.Add(GetExitToMainScreen(screenOverlay.ButtonElementList[0]));
            ButtonCollection = new ObservableCollection<ButtonElement>(screenOverlay.ButtonElementList);
            this.Height = screenOverlay.Height + 150;
            this.Width = screenOverlay.Width;
            this.Top = screenOverlay.Top;
            this.Left = screenOverlay.Left;
            WindowSizeProp = new WindowSizeProperties() { Height = screenOverlay.Height, Left = screenOverlay.Left, Top = screenOverlay.Top, Width = screenOverlay.Width };
        }

        private ButtonElement GetExitToMainScreen(ButtonElement firstElement)
        {
            var buttonelement = new ButtonElement();
            buttonelement.Left = firstElement.Left;
            buttonelement.Top = firstElement.Top - 100;
            buttonelement.Height = 50;
            buttonelement.Width = 250;
            buttonelement.Name = !string.IsNullOrEmpty(_mode) && _mode.ToLower().Equals("calibration") ? "Exit From Calibration" :  "Exit From Prediction";
            buttonelement.IsEnabled = true;
            buttonelement.ClickAction = ExitToMainMenu;
            buttonelement.BorderBrush = CustomBrushes.Red;
            return buttonelement;
        }

        private void ExitToMainMenu(object name)
        {
            if (!string.IsNullOrEmpty(_mode))
            {
                if (_mode.ToLower().Equals("prediction"))
                {
                    _predictionService.ForceExit();
                }
                else if (_mode.ToLower().Equals("calibration"))
                {
                    Environment.Exit(1);
                }
            }
        }
        private void NotificationFromUI(UIInstruction uIInstruction)
        {
            _calibrationService.ReceiveNotificationFromUI(uIInstruction);
            ClosePopup();
        }

       

        private void ClosePopup()
        {
            ShowPopup = false;
            PopupContentViewModel = null;
        }

        private void OpenPopup(object vm)
        {
            PopupContentViewModel = vm;
            ShowPopup = true;
        }

        private void OpenPopup()
        {
            ShowPopup = true;
        }

        public void UpdateTopMost()

        {
            Topmost = null;
            Topmost = new WindowTopMost();
        }
    }
}
