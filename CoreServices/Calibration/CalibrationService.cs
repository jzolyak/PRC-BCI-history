using CoreServices.Models;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Text;

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

        public CalibrationService()
        {
            _fileMetadataList = new ConcurrentDictionary<String,CalibrationFileMetadata>();
            _calibrationFilePath = System.IO.Path.GetDirectoryName(Assembly.GetEntryAssembly().Location)+ "\\CalibrationFiles\\";
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
                var files = Directory.GetFiles(_calibrationFilePath, "*.COEF");
                //filename pattern - usergivenname_setnum_status
                foreach (var file in files)
                {
                    if (string.IsNullOrEmpty(file)) continue;
                    var fileName = Path.GetFileName(file);
                    var lastUpdatedTime = File.GetLastWriteTime(file);
                    var metadata = fileName.Split("_");
                    if (metadata.Length < 3) continue;
                    if (_fileMetadataList.ContainsKey(metadata[0]))
                    {
                        var calibrationFileMetadata = _fileMetadataList[metadata[0]];
                        calibrationFileMetadata.NumofSets = calibrationFileMetadata.NumofSets + 1;
                        if (calibrationFileMetadata.LastUpdatedTime < lastUpdatedTime) calibrationFileMetadata.LastUpdatedTime = lastUpdatedTime;
                        if (metadata[2].ToLower().StartsWith("current"))
                        {
                            calibrationFileMetadata.CurrentSet = Int32.Parse(metadata[1]);
                            calibrationFileMetadata.IsCurrent = true;
                        }
                        if ( metadata.Length > 3)
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
                    }
                    else
                    {
                        int currentset = -1;
                        if (metadata[2].ToLower().Equals("current"))
                        {
                            currentset = Int32.Parse(metadata[1]);
                        }
                        _fileMetadataList.TryAdd(metadata[0], new CalibrationFileMetadata { UserGivenName = metadata[0], NumofSets=1,CurrentSet=currentset, IsStale = metadata.Length > 3 && metadata[3].ToLower().Equals("stale") });
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.Error(this, ex.Message);
            }
        }
        public void StartCalibration()
        {
           


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
        public void UserCancel()
        {

     
        }

        private void NotifyFileMetadataChange(CalibrationFileMetadata calibrationFileMetadata)
        {
            if (RaiseFileMetadataChange == null) return;
            RaiseFileMetadataChange(this,new EventArgs<CalibrationFileMetadata>(calibrationFileMetadata));
        }

        public void User_Actual_Start()
        {
            //run your script
        }
    }
}
