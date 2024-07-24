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
        public event EventHandler<EventArgs>? RaiseExitCalibrationEvent;
      

        private StateEngine _stateEngine;

        private PredictionFileMetadata _currentPredictionFile;
        private CalibrationFileMetadata _currentCalibrationFile;

        public CalibrationService()
        {
            _predictionMetadataList = new ConcurrentDictionary<String, PredictionFileMetadata>();
            _calibrationFilePath = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData) + "\\BCI\\CalibrationFiles\\";
            _stateEngine = new StateEngine(NotifyUI, NotifyScreenOverLayChange, ExitCalibration, OnCalibrationComplete,OnCalibrationPaused, ActivateUI);
            LoadCalibrationFiles();
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
            var calibrationFileMetadata = new CalibrationFileMetadata { UserGivenName = name, NumofSets = 5, CurrentSet = 1, IsCurrent = true, IsStale =false };
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
            MoveCurrentCalibrationFiles(_currentCalibrationFile.UserGivenName, destinationFolder);
            _currentCalibrationFile = null;
            NotifyFileMetadataChange(_currentCalibrationFile);
        }
        public void OnCalibrationComplete()
        {
            string fileName = _currentCalibrationFile.UserGivenName;
            int numOfSets = _currentCalibrationFile.NumofSets;
            UpdateCurrentCalibrationFileState("Completed");
            //Generate COEF file
            GenerateCOEFFile(fileName, numOfSets);
        }
        public void GenerateCOEFFile(string fileName, int numOfSets)
        {
            //Ensure required files are available. copy them from assembly directory to here if not.
            EnsureCoefGeneratorFilesAvailability();
            string cmdName = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData) + "\\BCI\\CalibrationFiles\\Completed\\script.bat";
            if (numOfSets <= 0)
            {
                Logger.Error(this, "number of sets are 0. unable to generate the coef file");
                return;
            }

            for (int setNum = 1; setNum <= numOfSets; setNum++)
            {
                cmdName = cmdName + " " + fileName + "_" + setNum + "_" + numOfSets;
            }
            cmdName = cmdName + " " + fileName;
            int ExitCode;
            ProcessStartInfo ProcessInfo;
            Process process;

            ProcessInfo = new ProcessStartInfo("cmd.exe", "/c " + cmdName);
            ProcessInfo.CreateNoWindow = true;
            ProcessInfo.UseShellExecute = false;
            ProcessInfo.WorkingDirectory = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData) + "\\BCI\\CalibrationFiles\\Completed";
            // *** Redirect the output ***
            ProcessInfo.RedirectStandardError = true;
            ProcessInfo.RedirectStandardOutput = true;

            process = Process.Start(ProcessInfo);
            process.WaitForExit();

            // *** Read the streams ***
            string output = process.StandardOutput.ReadToEnd();
            string error = process.StandardError.ReadToEnd();

            ExitCode = process.ExitCode;

            process.Close();
            Logger.Info(this, "output>>" + (String.IsNullOrEmpty(output) ? "(none)" : output));
            Logger.Info(this, "error>>" + (String.IsNullOrEmpty(error) ? "(none)" : error));
            Logger.Info(this, "ExitCode: " + ExitCode.ToString());
        }

        private void EnsureCoefGeneratorFilesAvailability()
        {
            string parentDirectory = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData) + "\\BCI\\CalibrationFiles\\Completed";
            string assemblyPath = Path.GetDirectoryName(Assembly.GetEntryAssembly().Location);
            string scriptFileName = "script.bat";
            string scriptFile = parentDirectory + "\\script.bat";
            string p300FileName = "P300Classifier_electrode_selection.exe";
            string p300File = parentDirectory + "\\P300Classifier_electrode_selection.exe";
            string iniFileName = "parameters_AAC-BCI.ini";
            string iniFile = parentDirectory + "\\parameters_AAC-BCI.ini";
            string headerFileName = "HeaderTemplate.txt";
            string headerFile = parentDirectory + "\\HeaderTemplate.txt";

            if (!File.Exists(scriptFile))
            {
                FileHandlerService.MoveFiles(scriptFileName, assemblyPath, parentDirectory);
            }
            if (!File.Exists(p300File))
            {
                FileHandlerService.MoveFiles(p300FileName, assemblyPath, parentDirectory);
            }
            if (!File.Exists(iniFile))
            {
                FileHandlerService.MoveFiles(iniFileName, assemblyPath, parentDirectory);
            }
            if (!File.Exists(headerFile))
            {
                FileHandlerService.MoveFiles(headerFileName, assemblyPath, parentDirectory);
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
