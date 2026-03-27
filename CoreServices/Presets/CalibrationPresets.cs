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
    public class CalibrationPresets:BasePresets
    {
        private static CalibrationPresets calibrationPresets;


        public static CalibrationPresets Instance
        {
            get { if (calibrationPresets == null) { calibrationPresets = new CalibrationPresets(); } return calibrationPresets; }
        }

        // Calibration preset categories
        public TargetAppearancePresets TargetAppearancePresets { get; private set; }
        public ModeControlPresets ModeControlPresets { get; private set; }

        private CalibrationPresets()
        {
            TargetAppearancePresets = new TargetAppearancePresets();
            ModeControlPresets = new ModeControlPresets();
            // Load initial settings
            LoadPresets();
        }
        
        protected override void LoadPresets()
        {
            var targetAppearanceSettings = presetsService.GetSettings(Constants.Calibration, "TargetAppearance");
            var modeControlSettings = presetsService.GetSettings(Constants.Calibration, "ModeControl");

            TargetAppearancePresets.RetreiveSettings(targetAppearanceSettings);
            ModeControlPresets.RetreiveSettings(modeControlSettings);
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

        public bool RetreiveSettings(Dictionary<string, string> settings)
        {
            if (settings == null || settings.Count == 0)
                return false;

            bool updated = false;

            foreach (var kvp in settings)
            {
                if (string.IsNullOrWhiteSpace(kvp.Key))
                    continue;

                // Normalize key for comparison
                string key = kvp.Key.Trim();

                // Clean the value → take only the part before ';'
                string rawValue = kvp.Value?.Trim() ?? string.Empty;
                string cleanValue = rawValue.Split(';')[0];

                if (key.Equals("user;bordercolor", StringComparison.OrdinalIgnoreCase))
                {
                    if (Enum.TryParse(cleanValue, true, out CustomBrushes brush))
                    {
                        HighLightON_BorderBrush = brush;
                        updated = true;
                    }
                    else
                    {
                        HighLightON_BorderBrush = CustomBrushes.Red; // fallback
                    }
                }
                else if (key.Equals("user;borderwidth", StringComparison.OrdinalIgnoreCase))
                {
                    if (int.TryParse(cleanValue, out int width) && width > 0)
                    {
                        borderWidth = width;
                        updated = true;
                    }
                    else
                    {
                        borderWidth = 3; // fallback
                    }
                }
            }

            return updated;
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

       
        public bool RetreiveSettings(Dictionary<string, string> settings)
        {
            if(settings == null) return false;
            foreach (KeyValuePair<string, string> kvp in settings)
            {
                if(string.IsNullOrEmpty(kvp.Key)) continue;
                if(kvp.Key.ToLower() == "user;numflashseqpertarget")
                {
                    try
                    {
                        numFlashSeqPerTarget = Convert.ToInt32(kvp.Value);
                    }
                    catch (Exception ex)
                    {
                        numFlashSeqPerTarget = 15;
                    }
                }
                if (kvp.Key.ToLower() == "user;scriptfilename")
                {
                    try
                    {
                        scriptFilename = Convert.ToString(kvp.Value);
                    }
                    catch (Exception ex)
                    {
                        scriptFilename = "CalibrationCommands.txt";
                    }
                }
            }
            return false;
        }
        
    }

}
