

using CoreServices;
using CoreServices.Calibration;
using CoreServices.Common;
using CoreServices.Models;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows.Input;
using ViewModels.Help;

namespace ViewModels.Calibration
{
    public class CalibrationViewModel : ExitToMainMenuViewModel
    {
        private CalibrationService _calibrationService;
        private UserSettingsService _userSettingsService;
        private IEnumerable<CalibrationFileMetadata> _calibrationFileMetadataColl;
        private ObservableCollection<CalibrationFileMetadata> _fileNamesCollection;
        private bool _isCalibrationInProgress;
        private bool _enableResume;
        private int _currentSet;
        private CalibrationFileMetadata _currentFileMetadata;
        private CalibrationFileMetadata _selectedFileMetadata;

        private DeleteConfirmationViewModel _deleteConfirmationViewModel;
        private FileNameEntryViewModel _fileNameEntryViewModel;
        private DemoVideoViewModel _demoVideoViewModel;
        private CalibrationHelpViewModel _calibrationHelpViewModel;
        private FlashStimulusViewModel _flashStimulusViewModel;
        private CalibrationInstructionsViewModel _calibrationInstructionsViewModel;
        private object? _popupContentViewModel;
        private bool _showPopup;
        private double _opacity;
        private bool _showDemoVideo;
        private int _currentFileMetadataIdx;
        private bool _hasFiles;
        public bool IsCalibrationInProgress { get { return _isCalibrationInProgress; } set { _isCalibrationInProgress = value; OnPropertyChanged(nameof(IsCalibrationInProgress)); } }
        public ICommand StartCmd { get; set; }
        public ICommand MoveUpCmd { get; set; }
        public ICommand MoveDownCmd { get; set; }
        public ICommand ResumeCmd { get; set; }
        public ICommand HelpCmd { get; set; }
        public ICommand SelectedFileChangeCmd { get; set; }

        public ObservableCollection<CalibrationFileMetadata> FileNamesCollection { get => _fileNamesCollection; set { _fileNamesCollection = value; OnPropertyChanged(nameof(FileNamesCollection)); } }
        public CalibrationFileMetadata CurrentFileMetadata { get => _currentFileMetadata; set { _currentFileMetadata = value; OnPropertyChanged(nameof(CurrentFileMetadata)); } }
        public CalibrationFileMetadata SelectedFileMetadata { get => _selectedFileMetadata; set { _selectedFileMetadata = value; OnPropertyChanged(nameof(SelectedFileMetadata)); } }
        public int CurrentSet { get => _currentSet; set { _currentSet = value; OnPropertyChanged(nameof(CurrentSet)); } }
        public bool EnableResume { get => _enableResume; set { _enableResume = value; OnPropertyChanged(nameof(EnableResume)); } }
        public bool HasFiles { get => _hasFiles;set { _hasFiles = value; OnPropertyChanged(nameof(HasFiles)); } }
        public bool ShowPopup { get => _showPopup; set { _showPopup = value; OnPropertyChanged(nameof(ShowPopup)); } }

        public double Opacity { get => _opacity; set { _opacity = value; OnPropertyChanged(nameof(Opacity)); } }

        public int CurrentFileMetadataIdx { set { _currentFileMetadataIdx = value; OnPropertyChanged(nameof(CurrentFileMetadataIdx)); } get { return _currentFileMetadataIdx; } }

        public object? PopupContentViewModel { get => _popupContentViewModel; set { _popupContentViewModel = value; OnPropertyChanged(nameof(PopupContentViewModel)); } }

        public CalibrationViewModel(Action exitAction,Action<bool> showorHideMainWindow,Action<ChildWindowSettings> showorHideChildWindow) : base(exitAction, showorHideMainWindow, showorHideChildWindow)
        {
            StartCmd = new RelayCommand(StartCalibrationPreCheck);
            ResumeCmd = new RelayCommand(ResumeCalibration);
            SelectedFileChangeCmd = new RelayCommand(OnSelectedFileChange);
            _calibrationService = ServiceLocator.CalibrationService;
            _calibrationService.RaiseFileMetadataChange += _calibrationService_RaiseFileMetadataChange;
            _calibrationService.RaiseExitCalibrationEvent += _calibrationService_RaiseExitCalibrationEvent;
            _userSettingsService = ServiceLocator.UserSettingsService;
            LoadCalibrationFilesMetadata();
            CurrentFileMetadata = FileNamesCollection.FirstOrDefault(x => x.IsCurrent);
            SelectedFileMetadata = CurrentFileMetadata ?? FileNamesCollection?.FirstOrDefault();
            CurrentSet = CurrentFileMetadata != default(CalibrationFileMetadata) && CurrentFileMetadata != null ? CurrentFileMetadata.CurrentSet : -1;
            EnableResume = CurrentFileMetadata != default(CalibrationFileMetadata) && CurrentFileMetadata != null;
            ShowPopup = false;
            PopupContentViewModel = null;
            _deleteConfirmationViewModel = new DeleteConfirmationViewModel(GetFileNameFromUser, ClosePopup, CurrentFileMetadata);
            _fileNameEntryViewModel = new FileNameEntryViewModel(DisplayDemoVideo, ClosePopup, _calibrationFileMetadataColl);
            _demoVideoViewModel = new DemoVideoViewModel(ShowCalibrationInstructions, ClosePopup);
            _calibrationInstructionsViewModel = new CalibrationInstructionsViewModel(StartActualCalibration, ClosePopup);
            _calibrationHelpViewModel = new CalibrationHelpViewModel(ClosePopup);
            _showDemoVideo = _userSettingsService.GetSettingsValue(Constants.DemoVideo_Preference_Key) == null ? true : !_userSettingsService.GetSettingsValue("ShowDemoVideo").ToLower().StartsWith("false");
            Opacity = 1;
            MoveUpCmd = new RelayCommand(MoveFileMetadataIndexUp);
            MoveDownCmd = new RelayCommand(MoveFileMetadataIndexDown);
            _flashStimulusViewModel = new FlashStimulusViewModel();
        }

        private void _calibrationService_RaiseExitCalibrationEvent(object? sender, EventArgs e)
        {
            if (_showorHideMainWindow != null) _showorHideMainWindow(true);
        }

        private void _calibrationService_RaiseFileMetadataChange(object? sender, EventArgs<CalibrationFileMetadata> e)
        {
            LoadCalibrationFilesMetadata();
            CurrentFileMetadata = FileNamesCollection.FirstOrDefault(x => x.IsCurrent);
            CurrentSet = CurrentFileMetadata != default(CalibrationFileMetadata) && CurrentFileMetadata != null ? CurrentFileMetadata.CurrentSet : -1;
            EnableResume = CurrentFileMetadata != default(CalibrationFileMetadata) && CurrentFileMetadata != null;

        }

        private void MoveFileMetadataIndexUp(object obj)
        {
            MoveFileMetadataIndex(true);
        }
        private void MoveFileMetadataIndexDown(object obj)
        {
            MoveFileMetadataIndex(false);
        }

        private void MoveFileMetadataIndex(bool moveUp)
        {
            int targetIdx = _currentFileMetadataIdx;
            if (moveUp)
            {
                if (_currentFileMetadataIdx <= 0) return;
                targetIdx = _currentFileMetadataIdx - 1;
            }
            else
            {
                if (_currentFileMetadataIdx >= FileNamesCollection.Count - 1) return;
                targetIdx = _currentFileMetadataIdx + 1;
            }
            //_currentFileMetadataIdx = targetIdx;
            SelectedFileMetadata = FileNamesCollection[targetIdx];
        }

        private void OnSelectedFileChange(object obj)
        {
            if (CurrentFileMetadata == null || obj == null) return;
            var selectedFileMetadata = (CalibrationFileMetadata)obj;
            if (!string.IsNullOrEmpty(CurrentFileMetadata.UserGivenName)  && selectedFileMetadata != null && !string.IsNullOrEmpty(selectedFileMetadata.UserGivenName))
            {
                if(!selectedFileMetadata.UserGivenName.ToLower().Equals(CurrentFileMetadata.UserGivenName))
                {
                    EnableResume = false;
                }
                else
                {
                    EnableResume = true;
                }
            }
            
        }

        private void ResumeCalibration(object obj)
        {
            
        }

        private void LoadCalibrationFilesMetadata()
        {
            if (_calibrationService == null) { Logger.Error(this, "CalibrationService is null"); return; }
            _calibrationFileMetadataColl = _calibrationService.GetCalibrationFilesMetadata();
            var sortedList = _calibrationFileMetadataColl.ToList();
            sortedList.Sort((one, two) => { return two.LastUpdatedTime.CompareTo(one.LastUpdatedTime); });
            FileNamesCollection = new ObservableCollection<CalibrationFileMetadata>(sortedList);
            HasFiles = FileNamesCollection.Count > 0;
        }

        private void CloseDemoView()
        {
            _showDemoVideo = _demoVideoViewModel.DemoVideoPreference;
            ClosePopup();
        }
        private void ClosePopup()
        {
            Opacity = 1;
            PopupContentViewModel = null;
            ShowPopup = false;
            if (_showorHideMainWindow != null) _showorHideMainWindow(true);
        }

        private void StartCalibrationPreCheck(object obj)
        {   
            //check to see if any current calibration in progress
            if(CurrentFileMetadata != default(CalibrationFileMetadata) && CurrentFileMetadata.NumofSets < 5)
            {
                DisplayPopup(_deleteConfirmationViewModel);
                return;
            }
            //get file name from user
            DisplayPopup(_fileNameEntryViewModel);
               
        }

        private void DisplayDemoVideo()
        {
            //check demo video preference 
            if (_showDemoVideo) {
                DisplayPopup(_demoVideoViewModel);
                return;
            }

            // StartActualCalibration();
            ShowCalibrationInstructions();
        }

        private void GetFileNameFromUser()
        {
            DisplayPopup(_fileNameEntryViewModel);
        }

        private void StartActualCalibration()
        {
            //if file is not available 
            if(string.IsNullOrEmpty(_fileNameEntryViewModel.FileName))
            {
                DisplayPopup(_fileNameEntryViewModel);
                return;
            }
           
            //for now create file with all 5 sets.
            CurrentFileMetadata = _calibrationService.CreateNewCalibrationFile(_fileNameEntryViewModel.FileName);
            PopupContentViewModel = null;
            ShowPopup = false;
            Opacity = 1;
            if (_showorHideMainWindow != null) _showorHideMainWindow(false);
            
            //start the calibration
            var childWindowpref = new ChildWindowSettings { ShowWindow = true, DataContext = _flashStimulusViewModel };
            _showorHideChildWindow(childWindowpref);
            _calibrationService.User_Actual_Start();

        }

        private void ShowCalibrationInstructions()
        {
            if (_showorHideMainWindow != null) _showorHideMainWindow(false);
            DisplayPopup(_calibrationInstructionsViewModel);
        }

       
        private void DisplayPopup(object contentViewModel)
        {
            Opacity = .1;
            PopupContentViewModel = contentViewModel;
            ShowPopup = true;
           
        }
    }
}
