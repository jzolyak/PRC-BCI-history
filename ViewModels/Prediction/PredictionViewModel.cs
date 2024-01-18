

using CoreServices;
using CoreServices.Calibration;
using CoreServices.Common;
using CoreServices.Models;
using CoreServices.Prediction;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows.Input;
using ViewModels.FlashStimulus;
using ViewModels.Help;

namespace ViewModels.Prediction
{
    public class PredictionViewModel : ExitToMainMenuViewModel
    {
        private PredictionService _predictionService;
        private UserSettingsService _userSettingsService;
        private IEnumerable<PredictionFileMetadata> _predictionFileMetadataColl;
        private ObservableCollection<PredictionFileMetadata> _fileNamesCollection;
        private bool _isCalibrationInProgress;
        private bool _enableResume;
        private int _currentSet;
        private int _numofSets;
        private string _calibrationName;
        private PredictionFileMetadata _currentFileMetadata;
        private PredictionFileMetadata _selectedFileMetadata;

        private FlashStimulusViewModel _flashStimulusViewModel;
        private PredictionInstructionsViewModel _predictionInstructionsViewModel;
        private object? _popupContentViewModel;
        private bool _showPopup;
        private bool _enablePredictionSelection;
        private double _opacity;
        private bool _showDemoVideo;
        private int _currentFileMetadataIdx;
        private bool _hasFiles;
        private bool _simulationModePreference;
        public bool IsCalibrationInProgress { get { return _isCalibrationInProgress; } set { _isCalibrationInProgress = value; OnPropertyChanged(nameof(IsCalibrationInProgress)); } }
        public ICommand StartCmd { get; set; }
        public ICommand MoveUpCmd { get; set; }
        public ICommand MoveDownCmd { get; set; }
        public ICommand HelpCmd { get; set; }
        public ICommand SelectedFileChangeCmd { get; set; }
        public ICommand ChangePredictionFileCmd { get; set; }
        public ICommand SimulationModePreferenceModeChangeCmd { get; set; }
        public bool IsInTablet { get; set; }
        public bool SimulationModePreference
        {
            get { return _simulationModePreference; }
            set { _simulationModePreference = value; OnPropertyChanged(nameof(SimulationModePreference)); }
        }

        public ObservableCollection<PredictionFileMetadata> FileNamesCollection { get => _fileNamesCollection; set { _fileNamesCollection = value; OnPropertyChanged(nameof(FileNamesCollection)); } }
        public PredictionFileMetadata SelectedFileMetadata { get => _selectedFileMetadata; set { _selectedFileMetadata = value; OnPropertyChanged(nameof(SelectedFileMetadata)); } }

        public PredictionFileMetadata CurrentFileMetadata { get => _currentFileMetadata; set { _currentFileMetadata = value; OnPropertyChanged(nameof(CurrentFileMetadata)); } }

        public bool HasFiles { get => _hasFiles;set { _hasFiles = value; OnPropertyChanged(nameof(HasFiles)); } }
        public bool ShowPopup { get => _showPopup; set { _showPopup = value; OnPropertyChanged(nameof(ShowPopup)); } }
        public bool EnablePredictionSelection { get => _enablePredictionSelection; set { _enablePredictionSelection = value; OnPropertyChanged(nameof(EnablePredictionSelection)); } }
        
        public double Opacity { get => _opacity; set { _opacity = value; OnPropertyChanged(nameof(Opacity)); } }

        public int CurrentFileMetadataIdx { set { _currentFileMetadataIdx = value; OnPropertyChanged(nameof(CurrentFileMetadataIdx)); } get { return _currentFileMetadataIdx; } }

        public object? PopupContentViewModel { get => _popupContentViewModel; set { _popupContentViewModel = value; OnPropertyChanged(nameof(PopupContentViewModel)); } }

        public PredictionViewModel(Action exitAction,Action<bool> showorHideMainWindow,Action<ChildWindowSettings> showorHideChildWindow) : base(exitAction, showorHideMainWindow, showorHideChildWindow)
        {
            StartCmd = new RelayCommand(StartPredictionPreCheck);
          
            SelectedFileChangeCmd = new RelayCommand(OnSelectedFileChange);
            ChangePredictionFileCmd = new RelayCommand(OnCurrentPredictionFileChange);
            SimulationModePreferenceModeChangeCmd = new RelayCommand(OnSimulationModePreferenceChange);
            _predictionService = ServiceLocator.PredictionService;
            _userSettingsService = ServiceLocator.UserSettingsService;
            LoadPredictionFilesMetadata();          
            ShowPopup = false;
            PopupContentViewModel = null;
            _predictionInstructionsViewModel = new PredictionInstructionsViewModel(StartActualPrediction, ClosePopup);
            Opacity = 1;
            
            _flashStimulusViewModel = new FlashStimulusViewModel();
        }

        private void OnSimulationModePreferenceChange(object obj)
        {
            _predictionService.UpdateSimulationModePreference(SimulationModePreference);
        }

        private void OnCurrentPredictionFileChange(object obj)
        {
            CurrentFileMetadata = SelectedFileMetadata;
            _predictionService.UpdateCurrentPredictionFileMetadata(CurrentFileMetadata);
            LoadPredictionFilesMetadata();
        }

        private void OnSelectedFileChange(object obj)
        {
           
        }

        private void StartPredictionPreCheck(object obj)
        {
            ShowPredictionInstructions();
        }

        
       
        private void LoadPredictionFilesMetadata()
        {
            if (_predictionService == null) { Logger.Error(this, "CalibrationService is null"); return; }
            _predictionFileMetadataColl = _predictionService.GetPredictionFilesMetadata();
            var sortedList = _predictionFileMetadataColl.ToList();
            sortedList.Sort((one, two) => { return two.LastUpdatedTime.CompareTo(one.LastUpdatedTime); });
            FileNamesCollection = new ObservableCollection<PredictionFileMetadata>(sortedList);
            HasFiles = FileNamesCollection.Count > 0;
            CurrentFileMetadata = _predictionService.GetCurrentPredictionFileMetadata();
            CurrentFileMetadataIdx = FileNamesCollection.IndexOf(CurrentFileMetadata);
        }
        private void ClosePopup()
        {
            Opacity = 1;
            PopupContentViewModel = null;
            ShowPopup = false;
            if (_showorHideMainWindow != null) _showorHideMainWindow(true);
        }


        private void StartActualPrediction()
        {
            //if file is not available 
        
            //for now create file with all 5 sets.
           // CurrentFileMetadata = _predictionService.CreateNewCalibrationFile(_fileNameEntryViewModel.FileName);
            PopupContentViewModel = null;
            ShowPopup = false;
            Opacity = 1;
            if (_showorHideMainWindow != null) _showorHideMainWindow(false);
            
            //start the calibration
            var childWindowpref = new ChildWindowSettings { ShowWindow = true, DataContext = _flashStimulusViewModel };
            _showorHideChildWindow(childWindowpref);
            _predictionService.User_Actual_Start();

        }

        private void ShowPredictionInstructions()
        {
            if (_showorHideMainWindow != null) _showorHideMainWindow(false);
            DisplayPopup(_predictionInstructionsViewModel);
        }

       
        private void DisplayPopup(object contentViewModel)
        {
            Opacity = .1;
            PopupContentViewModel = contentViewModel;
            ShowPopup = true;
           
        }
    }
}
