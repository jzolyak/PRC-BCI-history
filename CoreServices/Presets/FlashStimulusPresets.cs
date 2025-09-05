using CoreServices.Common;
using CoreServices.Models;
using CoreServices.Stimulus;
using System;
using System.Collections.Generic;
using System.Linq;

namespace CoreServices.Presets
{
    public class FlashStimulusPresets : BasePresets
    {
        private static readonly Lazy<FlashStimulusPresets> instance =
            new Lazy<FlashStimulusPresets>(() => new FlashStimulusPresets());

        //private readonly PresetsService presetsService;

        public static FlashStimulusPresets Instance => instance.Value;

        // Flash Stimulus preset categories
        public FlashAppearancePresets FlashAppearancePresets { get; private set; }
        public FlashTimingPresets FlashTimingPresets { get; private set; }

        private FlashStimulusPresets()
        {
            FlashAppearancePresets = new FlashAppearancePresets();
            FlashTimingPresets = new FlashTimingPresets();
            // Load initial presets
            LoadPresets();
        }

        protected override void LoadPresets()
        {
            var appearanceSettings = presetsService.GetSettings(Constants.FlashStimulus, "FlashAppearance");
            FlashAppearancePresets.RetreiveSettings(appearanceSettings);

            var timingSettings = presetsService.GetSettings(Constants.FlashStimulus, "FlashTiming");
            FlashTimingPresets.RetreiveSettings(timingSettings);
        }
    }

    public class FlashAppearancePresets
    {
        public enum FlashType : int { MonoColored = 0, ColoredPattern = 1, PicturePattern = 2 }

        public CustomBrushes FlashON_BorderBrush { get; set; }
        public CustomBrushes FlashON_BackgroundBrush { get; set; }
        public int borderWidth { get; set; }
        public FlashType flashType { get; set; }
        public string patternFilename { get; set; }

        public FlashAppearancePresets()
        {
            FlashON_BackgroundBrush = CustomBrushes.Brown;
            FlashON_BorderBrush = CustomBrushes.Yellow;
            borderWidth = 3;
            flashType = FlashType.MonoColored;
            patternFilename = "PictureList.txt";
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

                string key = kvp.Key.Trim().ToLower();
                string rawValue = kvp.Value?.Trim() ?? string.Empty;
                string cleanValue = rawValue.Split(';')[0]; // strip domain metadata if present

                switch (key)
                {
                    case "user;monoflashcolor":
                        if (Enum.TryParse(cleanValue, true, out CustomBrushes monoBrush))
                        {
                            FlashON_BackgroundBrush = monoBrush;
                            updated = true;
                        }
                        else
                        {
                            FlashON_BackgroundBrush = CustomBrushes.Brown; // fallback
                        }
                        break;

                    case "user;bordercolor":
                        if (Enum.TryParse(cleanValue, true, out CustomBrushes borderBrush))
                        {
                            FlashON_BorderBrush = borderBrush;
                            updated = true;
                        }
                        else
                        {
                            FlashON_BorderBrush = CustomBrushes.Yellow; // fallback
                        }
                        break;

                    case "user;borderwidth":
                        if (int.TryParse(cleanValue, out int width) && width > 0)
                        {
                            borderWidth = width;
                            updated = true;
                        }
                        else
                        {
                            borderWidth = 3; // fallback
                        }
                        break;

                    case "user;flashtype":
                        if (Enum.TryParse(cleanValue, true, out FlashType fType))
                        {
                            flashType = fType;
                            updated = true;
                        }
                        else
                        {
                            flashType = FlashType.MonoColored; // fallback
                        }
                        break;

                    case "user;patternfilename":
                        patternFilename = cleanValue;
                        updated = true;
                        break;
                }
            }

            return updated;
        }

    }

    public class FlashTimingPresets
    {
        public int timeToFindTarget { get; set; }
        public int timeBetweenTargets { get; set; }

        public FlashTimingPresets()
        {
            timeToFindTarget = 2;
            timeBetweenTargets = 4;
        }

        public bool RetreiveSettings(Dictionary<string, string> settings)
        {
            if (settings == null) return false;

            bool updated = false;

            foreach (var kvp in settings)
            {
                if (string.IsNullOrWhiteSpace(kvp.Key)) continue;

                switch (kvp.Key.ToLower())
                {
                    case "user;timetofindtarget":
                        try
                        {
                            timeToFindTarget = Convert.ToInt32(kvp.Value);
                            updated = true;
                        }
                        catch
                        {
                            timeToFindTarget = 2;
                        }
                        break;

                    case "user;timebetweentargets":
                        try
                        {
                            timeBetweenTargets = Convert.ToInt32(kvp.Value);
                            updated = true;
                        }
                        catch
                        {
                            timeBetweenTargets = 4;
                        }
                        break;
                }
            }

            return updated;
        }
    }
}
