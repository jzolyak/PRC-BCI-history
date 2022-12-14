using CoreServices.Calibration;
using CoreServices.Models;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using ViewModels.Help;

namespace ViewModels.Calibration
{
    public class FlashStimulusViewModel:ViewModelBase
    {
        private double _top;
        private double _left;
        private double _height;
        private double _width;
        private ObservableCollection<ButtonElement> _buttonCollection;
        private CalibrationService _calibrationService;
        private UIInstruction _uiInstruction;
        private object? _popupContentViewModel;
        private bool _showPopup;
        private CalibrationStopQueryViewModel _stopQueryViewModel;
        private CalibrationBeginQueryViewModel _beginQueryViewModel;
        private CalibrationCompleteViewModel _completeViewModel;
        private CalibrationRepeatQueryViewModel _repeatQueryViewModel;
        private CountDownViewModel _countDownViewModel;
        private WindowSizeProperties _windowSizeProp;


        public double Top { get { return _top; } set { _top = value; OnPropertyChanged(nameof(Top)); } }
        public double Left { get { return _left; } set { _left = value; OnPropertyChanged(nameof(Left)); } }
        public double Height { get { return _height; } set { _height = value; OnPropertyChanged(nameof(Height)); } }
        public double Width { get { return _width; } set { _width = value; OnPropertyChanged(nameof(Width)); } }
        public WindowSizeProperties WindowSizeProp { get => _windowSizeProp; set { _windowSizeProp = value; OnPropertyChanged(nameof(WindowSizeProp)); } }
        public ObservableCollection<ButtonElement> ButtonCollection { get { return _buttonCollection; } set { _buttonCollection = value; OnPropertyChanged(nameof(ButtonCollection)); } }
        public bool ShowPopup { get => _showPopup; set { _showPopup = value; OnPropertyChanged(nameof(ShowPopup)); } }
        public object? PopupContentViewModel { get => _popupContentViewModel; set { _popupContentViewModel = value; OnPropertyChanged(nameof(PopupContentViewModel)); } }

        public FlashStimulusViewModel()
        {
            _calibrationService = ServiceLocator.CalibrationService;
            _buttonCollection = new ObservableCollection<ButtonElement>();
            _calibrationService.RaiseScreenOverLayChange += _calibrationService_RaiseScreenOverLayChange;
            _calibrationService.RaiseUIRequestChange += _calibrationService_RaiseUIRequestChange;
            _stopQueryViewModel = new CalibrationStopQueryViewModel(NotificationFromUI);
            _beginQueryViewModel = new CalibrationBeginQueryViewModel(NotificationFromUI);
            _completeViewModel = new CalibrationCompleteViewModel();
            _repeatQueryViewModel = new CalibrationRepeatQueryViewModel(NotificationFromUI);
            _countDownViewModel = new CountDownViewModel(ClosePopup, NotificationFromUI);
            WindowSizeProp = null;
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
            if(e==null || e.Value == null) return;  
            var screenOverlay = e.Value;
            if (screenOverlay == null || screenOverlay.ButtonElementList == null) return;
            ButtonCollection = new ObservableCollection<ButtonElement>(screenOverlay.ButtonElementList);
            this.Height = screenOverlay.Height;
            this.Width = screenOverlay.Width;
            this.Top = screenOverlay.Top;
            this.Left = screenOverlay.Left;
            WindowSizeProp = new WindowSizeProperties() { Height = screenOverlay.Height, Left = screenOverlay.Left, Top = screenOverlay.Top, Width = screenOverlay.Width };
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
    }
}
