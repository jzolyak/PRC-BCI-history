using CoreServices.Files;
using CoreServices.Models;
using CoreServices.Stimulus;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
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

        private ConcurrentDictionary<String,PredictionFileMetadata> _predictionMetadataList;

        public event EventHandler<EventArgs<CalibrationFileMetadata>>? RaiseFileMetadataChange;

        public event EventHandler<EventArgs<ScreenOverlay>>? RaiseScreenOverLayChange;
        public event EventHandler<EventArgs<UIInstruction>>? RaiseUIRequestChange;
        public event EventHandler<EventArgs>? RaiseActiveUIEvent;
        public event EventHandler<EventArgs>? RaiseCoeffInProgress;
        public event EventHandler<EventArgs>? RaiseCoeffCompleted;
        public event EventHandler<EventArgs>? RaiseExitCalibrationEvent;
        public event EventHandler<EventArgs>? RaiseCoeffFailed;

        private StateEngine _stateEngine;

        private PredictionFileMetadata _currentPredictionFile;
        private CalibrationFileMetadata _currentCalibrationFile;
        private bool _caLibrationCompleted;
        private string _coeffFileName;
        private int _numofSets;

        public CalibrationService()
        {
            _predictionMetadataList = new ConcurrentDictionary<String, PredictionFileMetadata>();
            _calibrationFilePath = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData) + "\\BCI\\CalibrationFiles\\";
            _stateEngine = new StateEngine(NotifyUI, NotifyScreenOverLayChange, ExitCalibration, OnCalibrationComplete,OnCalibrationPaused, ActivateUI);
            _caLibrationCompleted= false;
            LoadCalibrationFiles();
            //GenerateCOEFFile("myfirstone", 5);
        }

        public IEnumerable<PredictionFileMetadata> GetPredictionFilesMetadata()
        {
            if(_predictionMetadataList == null || _predictionMetadataList.Count == 0) return new List<PredictionFileMetadata>();
           return _predictionMetadataList.Values;
        }
        public CalibrationFileMetadata GetCurrentCalibrationFileMetadata()
        {
            return _currentCalibrationFile;
        }
        private void LoadCalibrationFiles()
        {
            try
            {
                Logger.Info("special message");
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
                LoadPredictionFileMetadata(calfiles);

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
                if (_currentCalibrationFile == null)
                {
                    _currentCalibrationFile = new CalibrationFileMetadata
                    {
                        UserGivenName = metadata[0],
                        NumofSets = Int32.Parse(metadata[2]),
                        CurrentSet = Int32.Parse(metadata[1]),
                        IsCurrent = true,
                        FilePath=_calibrationFilePath
                    };
                }
                else
                {
                    if (!_currentCalibrationFile.UserGivenName.Equals(metadata[0]))
                    {
                        Logger.Error(this, "Multiple calibration file names exist. pleas check");
                    }
                    _currentCalibrationFile.UserGivenName = metadata[0];
                    _currentCalibrationFile.NumofSets = Int32.Parse(metadata[2]);
                    if(Int32.Parse(metadata[1]) > _currentCalibrationFile.CurrentSet)
                    {
                        _currentCalibrationFile.CurrentSet = Int32.Parse(metadata[1]);
                    }
                }
            }
        }

        private void LoadPredictionFileMetadata(string[] files)
        {
            foreach (var file in files)
            {
                if (string.IsNullOrEmpty(file)) continue;
                var fileName = Path.GetFileName(file);
                var lastUpdatedTime = File.GetLastWriteTime(file);
                var metadata = Path.GetFileNameWithoutExtension(fileName).Split("_");
                var fileName_NoExt = Path.GetFileNameWithoutExtension(fileName);
                var name = fileName_NoExt;
                if(fileName_NoExt.Contains("_current"))
                {
                    name = fileName_NoExt.Replace("_current", "").Trim();
                }
                var predictionFile = new PredictionFileMetadata
                {
                    Name = name,
                    FilePath = _calibrationFilePath
                };
                if (metadata.Length > 1 && metadata[metadata.Length-1].ToLower().Equals("current"))
                {
                    predictionFile.IsCurrent = true;
                    _currentPredictionFile = predictionFile;
                }
                _predictionMetadataList.TryAdd(metadata[0], predictionFile);
            }
        }
        public void StartCalibration()
        {
           


        }

        public bool CheckEmpowerIsAlive()
        {
            return _stateEngine ==null ? false : _stateEngine.CheckEmpowerIsAlive();
        }

      
        public CalibrationFileMetadata CreateNewCalibrationFile(string name)
        {
            var calibrationFileMetadata = new CalibrationFileMetadata { UserGivenName = name, NumofSets = 0, CurrentSet = 1, IsCurrent = true, IsStale =false };
            calibrationFileMetadata.FilePath = _calibrationFilePath;
            //NotifyFileMetadataChange(calibrationFileMetadata);
            _currentCalibrationFile = calibrationFileMetadata;
            NotifyFileMetadataChange(calibrationFileMetadata);
            return calibrationFileMetadata;
        }

        public void UserCancel()
        {

     
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
        private void ActivateUI()
        {
            if (RaiseActiveUIEvent == null) return;
            RaiseActiveUIEvent(this, EventArgs.Empty);           
        }

        private async void ExitCalibration()
        {
            bool isCOEFileGenerated = false;

            if (_caLibrationCompleted &&
                !string.IsNullOrEmpty(_coeffFileName) &&
                _numofSets > 0)
            {
                const int maxAttempts = 3;

                for (int attempt = 1; attempt <= maxAttempts; attempt++)
                {
                    RaiseCoeffInProgress?.Invoke(this, EventArgs.Empty);

                    isCOEFileGenerated = await GenerateCOEFFile(_coeffFileName, _numofSets);

                    if (isCOEFileGenerated)
                    {
                        var predictionFile = new PredictionFileMetadata
                        {
                            Name = _coeffFileName,
                            FilePath = _calibrationFilePath
                        };
                        _predictionMetadataList.TryAdd(_coeffFileName, predictionFile);

                        RaiseCoeffCompleted?.Invoke(this, EventArgs.Empty);
                        return;
                    }
                }

                // FAILED after 3 attempts
                RaiseCoeffFailed?.Invoke(this, EventArgs.Empty);
                return;
            }

            RaiseCoeffFailed?.Invoke(this, EventArgs.Empty);
        }

        public void ReceiveNotificationFromUI(UIInstruction instruction)           
        {
           _stateEngine.NotificationfromUI(instruction);
        }
        public void User_Actual_Start()
        {
            _caLibrationCompleted = false;
            _coeffFileName = string.Empty;
            _numofSets = 0;
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
            MoveCurrentCalibrationFiles(_currentCalibrationFile.UserGivenName, destinationFolder);
            _currentCalibrationFile = null;
            NotifyFileMetadataChange(_currentCalibrationFile);
        }
        public void OnCalibrationComplete()
        {
            _coeffFileName = _currentCalibrationFile.UserGivenName;
            _numofSets = _currentCalibrationFile.NumofSets;
            
            UpdateCurrentCalibrationFileState("Completed");
            _caLibrationCompleted = true;
            //Generate COEF file
            

        }
        public async Task<bool> GenerateCOEFFile(string fileName, int numOfSets)
        {
            try
            {
                const int timeout = 10000;

                if (numOfSets <= 0)
                {
                    Logger.Error(this, "Number of sets is 0 or invalid. Unable to generate COEF file.");
                    return false;
                }

                string calibrationFilesFolderPath =
                    Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                                 "BCI", "CalibrationFiles");

                string completedFolder =
                    Path.Combine(calibrationFilesFolderPath, "Completed");

                string spacelessFileName = fileName.Replace(' ', '_');

                // --------------------------------------------------
                // Validate all required BIN files exist
                // --------------------------------------------------
                for (int setNum = 1; setNum <= numOfSets; setNum++)
                {
                    string binFilePath = Path.Combine(
                        completedFolder,
                        $"{fileName}_{setNum}_{numOfSets}.bin");

                    if (!File.Exists(binFilePath))
                    {
                        Logger.Error(this, $"Missing calibration BIN file: {binFilePath}");
                        return false;
                    }
                }

                // --------------------------------------------------
                // Build batch command
                // --------------------------------------------------
                string baseCommand =
                    "\"\"" + Path.Combine(
                        Path.GetDirectoryName(Assembly.GetEntryAssembly().Location),
                        "script.bat") + "\"";

                baseCommand += $" \"{spacelessFileName}\"";

                for (int setNum = 1; setNum <= numOfSets; setNum++)
                {
                    baseCommand += $" \"{fileName}_{setNum}_{numOfSets}\"";
                }

                baseCommand += "\"";

                string finalCoefFilePath =
                    Path.Combine(calibrationFilesFolderPath, fileName + ".coef");

                // --------------------------------------------------
                // Start classifier process
                // --------------------------------------------------
                ProcessStartInfo processInfo = new ProcessStartInfo(
                    "cmd.exe", "/c " + baseCommand)
                {
                    CreateNoWindow = true,
                    UseShellExecute = false,
                    WorkingDirectory = completedFolder,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true
                };

                using (Process process = Process.Start(processInfo))
                {
                    if (!process.WaitForExit(timeout))
                    {
                        Logger.Error(this, "Classifier process timed out.");
                        try { process.Kill(); } catch { }
                        return false;
                    }

                    string stdout = process.StandardOutput.ReadToEnd();
                    string stderr = process.StandardError.ReadToEnd();

                    if (!string.IsNullOrWhiteSpace(stderr))
                    {
                        Logger.Error(this, $"Classifier error output: {stderr}");
                    }

                    if (!string.IsNullOrWhiteSpace(stdout))
                    {
                        Logger.Info(this, $"Classifier output: {stdout}");
                    }
                }

                // --------------------------------------------------
                // Rename spaceless COEF file (if needed)
                // --------------------------------------------------
                FileHandlerService.RenameFile(
                    spacelessFileName,
                    fileName,
                    calibrationFilesFolderPath);

                // --------------------------------------------------
                // Final validation
                // --------------------------------------------------
                return File.Exists(finalCoefFilePath);
            }
            catch (Exception ex)
            {
                Logger.Error(this, $"GenerateCOEFFile failed: {ex}");
                return false;
            }
        }



        public void OnCalibrationPaused()
        {
            NotifyFileMetadataChange(_currentCalibrationFile);
        }

        public void ForceExit()
        {
            _stateEngine.ForceExit();
        }
    }

   
}
