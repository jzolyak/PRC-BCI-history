using CoreServices.Common;
using CoreServices.Models;
using CoreServices.Stimulus;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CoreServices.Presets
{
    public class CalibrationPresets
    {
        private static CalibrationPresets calibrationPresets;

        private PresetsService presetsService;

        public static CalibrationPresets Instance
        {
            get { if (calibrationPresets == null) { calibrationPresets = new CalibrationPresets(); } return calibrationPresets; }
        }

        // Calibration preset categories
        public TargetAppearancePresets TargetAppearancePresets { get; private set; }
        public ModeControlPresets ModeControlPresets { get; private set; }

        private CalibrationPresets()
        {
            presetsService = PresetsService.Instance;
            TargetAppearancePresets = new TargetAppearancePresets();
            ModeControlPresets = new ModeControlPresets();

            TargetAppearancePresets.RetreiveSettings(presetsService.GetSettings(Constants.Flash, "TargetAppearance"));
            //ModeControlPresets.RetreiveSettings(presetsService.GetSettings(Constants.Flash, "ModeControl"));
        }
    }

    public class TargetAppearancePresets
    {
        public CustomBrushes HighLightON_BorderBrush { get; set; }

        public int borderWidth { get; set; }                // Sekar: Can this be a preset or must it be a constant in XAML page?


        public TargetAppearancePresets()
        {
            HighLightON_BorderBrush = CustomBrushes.Red;
            borderWidth = 3;                                // Default=3, Range={3, 7}, Size in pixels
        }

        public bool RetreiveSettings(Dictionary<string,string> settings)
        {
            if (settings == null) return false;

            foreach (KeyValuePair<string,string> kvp in settings)
            {
                if (string.IsNullOrEmpty(kvp.Key)) continue;
                
                if (kvp.Key.ToLower() == "user;highlightcolor")
                {
                    try
                    {
                        HighLightON_BorderBrush = (CustomBrushes)Enum.Parse(typeof(CustomBrushes), kvp.Value);
                    }
                    catch (Exception ex)
                    {
                        HighLightON_BorderBrush = CustomBrushes.Red;
                    }
                }
            }
            return false;
        }
    }

    public class ModeControlPresets    {
        public int numFlashSeqPerTarget { get; set; }
        public String scriptFilename { get; set; }

        public ModeControlPresets()
        {
            numFlashSeqPerTarget = 15;                      // Default=15, Range={4, 15}, Number flash sequences to collect raw data
            scriptFilename = "CalibrationCommands.txt";     // Script controls which target keys are used in Calibration mode
        }

        /*
        public bool RetreiveSettings(Dictionary<string, string> settings)
        {
        }
        */
    }

}
