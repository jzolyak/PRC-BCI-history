using CoreServices.Common;
using CoreServices.Models;
using CoreServices.Stimulus;
using CoreServices.Presets;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CoreServices.Presets
{
    public class FlashStimulusPresets
    {
        private static FlashStimulusPresets flashStimulusPresets;

        private PresetsService presetsService;

        public static FlashStimulusPresets Instance
        {
            get { if (flashStimulusPresets == null) { flashStimulusPresets = new FlashStimulusPresets(); } return flashStimulusPresets; }
        }

        // Flash Stimulus preset categories
        public FlashAppearancePresets FlashAppearancePresets { get; private set; }
        public FlashTimingPresets FlashTimingPresets { get; private set; }

        private FlashStimulusPresets()
        {
            presetsService = PresetsService.Instance;
            FlashAppearancePresets = new FlashAppearancePresets();
            FlashTimingPresets = new FlashTimingPresets();

            FlashAppearancePresets.RetreiveSettings(presetsService.GetSettings(Constants.Flash, "FlashAppearance"));

            // Sekhar???  Don't we need a RetrieveSettings() method for timing presets???
            // FlashTimingPresets.RetreiveSettings(presetsService.GetSettings(Constants.Flash, "FlashTiming"));
        }
    }

    public class FlashAppearancePresets
    {
        public enum FlashType : int { MonoColored = 0, ColoredPattern = 1, PicturePattern = 2 };

        //public CustomBrushes HighLightON_BorderBrush { get; set; }    // DELETE THIS HERE! THIS PRESET BELONGS TO CALIBRATION PRESETS!

        public CustomBrushes FlashON_BorderBrush { get; set; }
        public CustomBrushes FlashON_BackgroundBrush { get; set; }

        public int borderWidth { get; set; }                    // Sekar: Can this be a preset or must it be a constant in XAML page?
        public FlashType flashType { get; set; }
        public String patternFilename { get; set; }

        public FlashAppearancePresets()
        {
            //HighLightON_BorderBrush = CustomBrushes.Red;      // DELETE THIS HERE! THIS PRESET BELONGS TO CALIBRATION PRESETS!

            FlashON_BackgroundBrush = CustomBrushes.Brown;
            FlashON_BorderBrush = CustomBrushes.Yellow;

            borderWidth = 3;                                    // Default=3, Range={3, 7}, Size in pixels
            flashType = FlashType.MonoColored;                  // Default=MonoColored, Range={monoColor, colorPattern, picturePattern}
            patternFilename = "PictureList.txt";                // Default=PictureList.txt, Range={any text string for filename containing pictures}
        }

        public bool RetreiveSettings(Dictionary<string,string> settings)
        {
            if (settings == null) return false;

            foreach (KeyValuePair<string,string> kvp in settings)
            {
                if (string.IsNullOrEmpty(kvp.Key)) continue;

                if (kvp.Key.ToLower() == "user;flashcolor")
                {
                    try
                    {
                        FlashON_BackgroundBrush = (CustomBrushes)Enum.Parse(typeof(CustomBrushes), kvp.Value);
                    }
                    catch(Exception ex)
                    {
                        FlashON_BackgroundBrush = CustomBrushes.Brown;
                    }
                }

                /*
                if (kvp.Key.ToLower() == "user;highlightcolor")     // DELETE THIS HERE! THIS PRESET BELONGS TO CALIBRATION PRESETS!
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
                */
            }
            return false;
        }
    }

    public class FlashTimingPresets
    {
        public int timeToFindTarget { get; set; }
        public int timeBetweenTargets { get; set; }

        public FlashTimingPresets()
        {
            timeToFindTarget = 2;           // Default=2, Range={2, 5}, Time delay in sec to locate next target
            timeBetweenTargets = 4;         // Default=4, Range={2, 5}, Time delay in sec before start of next selection
        }

        /*
        public bool UpdateBackgroundBrush(String name)      // DOES THIS REALLY BELONG HERE AT ALL? If not, remove
        {
            return true;
        }
        */

        /*
        public bool RetreiveSettings(Dictionary<string, string> settings)
        {
        }
        */

    }

}
