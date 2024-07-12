using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CoreServices.Models
{
    public class PredictionFileMetadata
    {
        public string Name { get; set; }
        public bool IsCurrent { get ; set; }
        public DateTime LastUpdatedTime { get ; set; }
        public string FilePath { get; set; }
    
        public override string ToString()
        {
            if (IsCurrent) return Name + "(Current)";
            else return Name;
        }

    }
}
