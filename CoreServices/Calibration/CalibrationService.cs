using CoreServices.Files;
using CoreServices.Models;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace CoreServices.Calibration
{
    public class CalibrationService
    {
        private string _calibrationFilePath;
        //Calibration service responsible for following 
        // - Maintain the calibration status - file management and also current set etc
        // - Dictate the calibration steps when calibration is in progress - core loop
        // - Talks to Headset to record the data while calibration is in progress

        private ConcurrentDictionary<String,CalibrationFileMetadata> _fileMetadataList;

        public event EventHandler<EventArgs<CalibrationFileMetadata>>? RaiseFileMetadataChange;

        public event EventHandler<EventArgs<ScreenOverlay>>? RaiseScreenOverLayChange;
        public event EventHandler<EventArgs<UIInstruction>>? RaiseUIRequestChange;
        public event EventHandler<EventArgs>? RaiseExitCalibrationEvent;
      

        private StateEngine _stateEngine;

        private CalibrationFileMetadata _currentPredictionFile;
        private CalibrationFileMetadata _currentCalibrationFile;

        public CalibrationService()
        {
            _fileMetadataList = new ConcurrentDictionary<String,CalibrationFileMetadata>();
            _calibrationFilePath = System.IO.Path.GetDirectoryName(Assembly.GetEntryAssembly().Location)+ "\\CalibrationFiles\\";
            _stateEngine = new StateEngine(NotifyUI, NotifyScreenOverLayChange, ExitCalibration, OnCalibrationComplete);
            LoadCalibrationFiles();
        }

        public IEnumerable<CalibrationFileMetadata> GetCalibrationFilesMetadata()
        {
            if(_fileMetadataList==null || _fileMetadataList.Count == 0) return new List<CalibrationFileMetadata>();
           return _fileMetadataList.Values;
        }
        private void LoadCalibrationFiles()
        {
            try
            {
                //Load the calibration files from local disk
                if (!Directory.Exists(_calibrationFilePath))
                {
                    Logger.Error(this, "Cannot find Calibration files directory. Unable to load calibration files"); return;
                }
                var files = Directory.GetFiles(_calibrationFilePath, "*.bin");
                //filename pattern - usergivenname_setnum_status
                LoadCalibrationFileMetadata(files);
                var calfiles = Directory.GetFiles(_calibrationFilePath, "*.COEF");
                //filename pattern - usergivenname_setnum_status
                LoadCalibrationFileMetadata(calfiles);

            }
            catch (Exception ex)
            {
                Logger.Error(this, ex.Message);
            }
        }

        private void LoadCalibrationFileMetadata(string[] files)
        {
            foreach (var file in files)
            {
                if (string.IsNullOrEmpty(file)) continue;
                var fileName = Path.GetFileName(file);
                var lastUpdatedTime = File.GetLastWriteTime(file);
                var metadata = Path.GetFileNameWithoutExtension(fileName).Split("_");
                if (metadata.Length < 3) continue;
                if (_fileMetadataList.ContainsKey(metadata[0]))
                {
                    var calibrationFileMetadata = _fileMetadataList[metadata[0]];
                    if (calibrationFileMetadata.LastUpdatedTime < lastUpdatedTime) calibrationFileMetadata.LastUpdatedTime = lastUpdatedTime;
                    var thirdParam = metadata[2].ToLower();
                    if (thirdParam.StartsWith("current"))
                    {
                        calibrationFileMetadata.CurrentSet = Int32.Parse(metadata[1]);
                        calibrationFileMetadata.IsCurrent = true;                     
                    }
                    else if (thirdParam.StartsWith("prediction"))
                    {
                        calibrationFileMetadata.IsCurrentPredictionFile = true;
                        _currentPredictionFile = calibrationFileMetadata;
                    }
                    if( file.EndsWith(".bin"))
                    {
                        int setNum = Int32.Parse(metadata[1]);
                        if (setNum > calibrationFileMetadata.CurrentSet)
                        {
                            calibrationFileMetadata.CurrentSet = setNum;
                        }
                        calibrationFileMetadata.IsCurrent = true;
                    }
                    if (metadata.Length > 3)
                    {
                        if (metadata[3].ToLower().Equals("stale"))
                        {
                            calibrationFileMetadata.IsStale = true;
                        }
                        else if (metadata[3].ToLower().Equals("currentfile"))
                        {
                            calibrationFileMetadata.IsCurrent = true;
                        }
                        //revisit - need to deal with stale
                    }
                    if (file.EndsWith(".bin"))
                    {
                        _currentCalibrationFile = calibrationFileMetadata;
                    }
                }
                else
                {
                   
                    _currentCalibrationFile = new CalibrationFileMetadata
                    {
                        UserGivenName = metadata[0],
                        NumofSets = Int32.Parse(metadata[2]),
                        CurrentSet = Int32.Parse(metadata[1]),
                        IsCurrent = true,
                        IsStale = metadata.Length > 3 && metadata[3].ToLower().Equals("stale")
                    };
                    _fileMetadataList.TryAdd(metadata[0], _currentCalibrationFile);
                }
            }
        }
        public void StartCalibration()
        {
           


        }

        public bool CheckEmpowerIsAlive()
        {
            return _stateEngine ==null ? false : _stateEngine.CheckEmpowerIsAlive();
        }

        public CalibrationFileMetadata CreateDummyCalibrationFiles(string name)
        {
            //usergivenname_setnum_status
            string fileName1 = Path.Combine(_calibrationFilePath, name + "_1_Complete.COEF");
            string fileName2 = Path.Combine(_calibrationFilePath, name + "_2_Complete.COEF");
            string fileName3 = Path.Combine(_calibrationFilePath, name + "_3_Complete.COEF");
            string fileName4 = Path.Combine(_calibrationFilePath, name + "_4_Complete.COEF");
            string fileName5 = Path.Combine(_calibrationFilePath, name + "_5_Complete.COEF");
            File.WriteAllLines(fileName1, new string[] { "Test" });
            File.WriteAllLines(fileName2, new string[] { "Test" });
            File.WriteAllLines(fileName3, new string[] { "Test" });
            File.WriteAllLines(fileName4, new string[] { "Test" });
            File.WriteAllLines(fileName5, new string[] { "Test" });
            var calibrationFileMetadata = new CalibrationFileMetadata { UserGivenName = name, NumofSets = 5, CurrentSet = 5, IsStale = false };
            _fileMetadataList.TryAdd(_calibrationFilePath, calibrationFileMetadata);
            NotifyFileMetadataChange(calibrationFileMetadata);
            return calibrationFileMetadata;
        }

        public CalibrationFileMetadata CreateNewCalibrationFile(string name)
        {
            var calibrationFileMetadata = new CalibrationFileMetadata { UserGivenName = name, NumofSets = 5, CurrentSet = 1, IsStale = false };
            _fileMetadataList.TryAdd(_calibrationFilePath, calibrationFileMetadata);
            //NotifyFileMetadataChange(calibrationFileMetadata);
            _currentCalibrationFile = calibrationFileMetadata;
            NotifyFileMetadataChange(calibrationFileMetadata);
            return calibrationFileMetadata;
        }

        public void UserCancel()
        {

     
        }

        public void UpdatePredictionFile(CalibrationFileMetadata newFile)
        {
            if (newFile == null || string.IsNullOrEmpty(newFile.UserGivenName) || !newFile.IsCurrentPredictionFile || _currentPredictionFile.UserGivenName.ToLower().Equals(newFile.UserGivenName.ToLower())) return;
            //rename filenames or use another calibration settings file to store the current prediction file name
            
        }

        private void NotifyFileMetadataChange(CalibrationFileMetadata calibrationFileMetadata)
        {
            if (RaiseFileMetadataChange == null) return;
            RaiseFileMetadataChange(this,new EventArgs<CalibrationFileMetadata>(calibrationFileMetadata));
        }

        private void NotifyScreenOverLayChange(ScreenOverlay screenOverlay)
        {
            if(RaiseScreenOverLayChange == null) return;
            RaiseScreenOverLayChange(this,new EventArgs<ScreenOverlay>(screenOverlay));
            
        }

        private void NotifyUI(UIInstruction uiInsturction)
        {
            if(RaiseUIRequestChange == null) return;
            RaiseUIRequestChange(this, new EventArgs<UIInstruction>(uiInsturction));
        }

        private void ExitCalibration()
        {
            if (RaiseExitCalibrationEvent != null) RaiseExitCalibrationEvent(this, new EventArgs());
        }
        public void ReceiveNotificationFromUI(UIInstruction instruction)
        {
           _stateEngine.NotificationfromUI(instruction);
        }
        public void User_Actual_Start()
        {
            _stateEngine.SetCalibrationFile(_currentCalibrationFile);
            Task.Run(_stateEngine.CalibrationMode);
        }

        public void MoveCurrentCalibrationFiles(string userGivenName,string destinationFolder)
        {
            if(_currentCalibrationFile == null || string.IsNullOrEmpty(userGivenName)) return;
            if(!userGivenName.Equals(_currentCalibrationFile.UserGivenName))
            {
                Logger.Error(this, "Unable to delete the calibration file:" + userGivenName);
                return;
            }
            var files = Directory.GetFiles(_calibrationFilePath, userGivenName+"*.bin");
            foreach (var file in files)
            {
                var fileName = Path.GetFileName(file);
                FileHandlerService.MoveFiles(fileName, _calibrationFilePath, destinationFolder);
            }
        }
        public void UpdateCurrentCalibrationFileState(string destinationFolder)
        {
            CalibrationFileMetadata calibrationFileMetadata = null;
            _fileMetadataList.TryRemove(_currentCalibrationFile.UserGivenName, out calibrationFileMetadata);
            MoveCurrentCalibrationFiles(_currentCalibrationFile.UserGivenName, destinationFolder);
            _currentCalibrationFile = null;
            NotifyFileMetadataChange(_currentCalibrationFile);
        }
        public void OnCalibrationComplete()
        {
            UpdateCurrentCalibrationFileState("Completed");
        }
    }

   
}
