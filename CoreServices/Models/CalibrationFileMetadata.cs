using System;
using System.Collections.Generic;
using System.Text;

namespace CoreServices.Models
{
    public class CalibrationFileMetadata
    {
        private string _userGivenName;
        private int _numofSets;
        private bool isStale;        
        private int _currentSet;
        private bool isCurrent;
        private bool isCurrentPredictionFile;
        private DateTime lastUpdatedTime;
        public bool IsStale { get => isStale; set => isStale = value; }
        public string UserGivenName { get => _userGivenName; set => _userGivenName = value; }
        public int NumofSets { get => _numofSets; set => _numofSets = value; }
        public int CurrentSet { get => _currentSet; set => _currentSet = value; }
        public bool IsCurrent { get => isCurrent; set => isCurrent = value; }
        public DateTime LastUpdatedTime { get => lastUpdatedTime; set => lastUpdatedTime = value; }
        public bool IsCurrentPredictionFile { get => isCurrentPredictionFile; set => isCurrentPredictionFile = value; }

        public override string ToString()
        {
            if (isCurrent) return UserGivenName + "(Current)";
            else return UserGivenName;
        }
    }
}
