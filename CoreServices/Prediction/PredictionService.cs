using CoreServices.Files;
using CoreServices.Models;
using CoreServices.Stimulus;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace CoreServices.Prediction
{

    public class PredictionService
    {
        private string _predictionFilePath;
        private bool _inSimulationMode;

        private ConcurrentDictionary<String, PredictionFileMetadata> _predictionMetadataList;
        private PredictionStateEngine _predictionStateEngine;
        private PredictionFileMetadata _currentPredictionFile;

        public event EventHandler<EventArgs<ScreenOverlay>>? RaiseScreenOverLayChange;
        public event EventHandler<EventArgs<UIInstruction>>? RaiseUIRequestChange;
        public event EventHandler<EventArgs>? RaiseExitPredictionEvent;

        public PredictionService()
        {
            _predictionMetadataList = new ConcurrentDictionary<String, PredictionFileMetadata>();
            _predictionFilePath = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData) + "\\BCI\\CalibrationFiles\\";
            _predictionStateEngine = new PredictionStateEngine(NotifyUI, NotifyScreenOverLayChange, ExitPrediction, null, null);
            LoadPredictionFiles();
        }

        private void LoadPredictionFiles()
        {
            try
            {
                //Load the calibration files from local disk
                if (!Directory.Exists(_predictionFilePath))
                {
                    Logger.Error(this, "Cannot find Calibration files directory. Unable to load calibration files"); return;
                }

                var calfiles = Directory.GetFiles(_predictionFilePath, "*.COEF");
                //filename pattern - usergivenname_setnum_status
                LoadPredictionFileMetadata(calfiles);

            }
            catch (Exception ex)
            {
                Logger.Error(this, ex.Message);
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

                var predictionFile = new PredictionFileMetadata
                {
                    Name = metadata[0],
                    FilePath = _predictionFilePath
                };
                if (metadata.Length > 1 && metadata[1].ToLower().Equals("current"))
                {
                    predictionFile.IsCurrent = true;
                    _currentPredictionFile = predictionFile;
                }
                _predictionMetadataList.TryAdd(metadata[0], predictionFile);
            }
        }

        public void User_Actual_Start()
        {
            _predictionStateEngine.SetPredictionFile(_currentPredictionFile);
            Task.Run(_predictionStateEngine.PredictionMode);
        }

        public IEnumerable<PredictionFileMetadata> GetPredictionFilesMetadata()
        {
            if (_predictionMetadataList == null || _predictionMetadataList.Count == 0) return new List<PredictionFileMetadata>();
            return _predictionMetadataList.Values;
        }

        private void NotifyScreenOverLayChange(ScreenOverlay screenOverlay)
        {
            if (RaiseScreenOverLayChange == null) return;
            RaiseScreenOverLayChange(this, new EventArgs<ScreenOverlay>(screenOverlay));

        }

        private void NotifyUI(UIInstruction uiInsturction)
        {
            if (RaiseUIRequestChange == null) return;
            RaiseUIRequestChange(this, new EventArgs<UIInstruction>(uiInsturction));
        }

        private void ExitPrediction()
        {
            if (RaiseExitPredictionEvent != null) RaiseExitPredictionEvent(this, new EventArgs());
        }

        public PredictionFileMetadata GetCurrentPredictionFileMetadata()
        {
            return _currentPredictionFile;
        }
        public void UpdateCurrentPredictionFileMetadata(PredictionFileMetadata newFile)
        {
            if(_currentPredictionFile!=null)
            {
                _currentPredictionFile.IsCurrent = false;
                FileHandlerService.RenameFile(_currentPredictionFile.Name + "_" + "current.COEF", _currentPredictionFile.Name + ".COEF", _predictionFilePath);
            }
            
            FileHandlerService.RenameFile(newFile.Name+ ".COEF", newFile.Name + "_" + "current.COEF", _predictionFilePath);
            _currentPredictionFile = newFile;
            _currentPredictionFile.IsCurrent = true;
            
        }

        public void UpdateSimulationModePreference(bool simulationMode)
        {
            _inSimulationMode = simulationMode;
        }
        public void ForceExit()
        {
            _predictionStateEngine.ForceExit();
        }


    }
}
