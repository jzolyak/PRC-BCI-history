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
    public class FlashStimulusPresets
    {
        private static FlashStimulusPresets flashStimulusPresets;

        private PresetsService presetsService;

        public static FlashStimulusPresets Instance
        {
            get { if (flashStimulusPresets == null) { flashStimulusPresets = new FlashStimulusPresets(); } return flashStimulusPresets; }

        }

        public FlashAppearancePresets FlashAppearancePresets { get; private set; }
        public FlashTimingPresets FlashTimingPresets { get; private set; }
        private FlashStimulusPresets()
        {
            presetsService = PresetsService.Instance;
            FlashAppearancePresets = new FlashAppearancePresets();
            FlashTimingPresets = new FlashTimingPresets();
            FlashAppearancePresets.RetreiveSettings(presetsService.GetSettings(Constants.Flash, "FlashAppearance"));
        }

      
    }
    public class FlashAppearancePresets
    {
        public CustomBrushes HighLightON_BorderBrush { get; set; }
        public CustomBrushes FlashON_BorderBrush { get; set; }
        public CustomBrushes FlashON_BackgroundBrush { get; set; }


        public FlashAppearancePresets()
        {
            FlashON_BackgroundBrush = CustomBrushes.Brown;
            HighLightON_BorderBrush = CustomBrushes.Red;
            FlashON_BorderBrush = CustomBrushes.Yellow;
        }
        public bool RetreiveSettings(Dictionary<string,string> settings)
        {
            if(settings == null) return false;
            foreach(KeyValuePair<string,string> kvp in settings)
            {
                if (string.IsNullOrEmpty(kvp.Key)) continue;
               if(kvp.Key.ToLower()=="user;flashcolor")
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

    public class FlashTimingPresets
    {
        public int TimeBetweenTargets { get; set; }
     
        public bool UpdateBackgroundBrush(String name)
        {
            return true;
        }
    }

}
