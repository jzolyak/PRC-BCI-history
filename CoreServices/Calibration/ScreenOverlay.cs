using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CoreServices.Calibration
{
    public class ScreenOverlay
    {
        public double Height { get; set; }
        public double Width { get; set; }
        public List<ButtonElement>? ButtonElementList { get; set; }

        public ScreenOverlay()

        {
            ButtonElementList = new List<ButtonElement>();  
        }
    }
}
