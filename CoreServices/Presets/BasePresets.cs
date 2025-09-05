using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CoreServices.Presets
{
    public abstract class BasePresets
    {
        protected PresetsService presetsService;

        public BasePresets()
        {
            presetsService = PresetsService.Instance;

            // Subscribe to settings change
            //UserSettingsService.Instance.UserSettingsChanged += OnUserSettingsChanged;
            PresetsService.Instance.PresetsSettingsChanged += OnPresetSettingsChanged;
        }
        private void OnPresetSettingsChanged(object sender, EventArgs e)
        {
            LoadPresets();
        }

        protected abstract void LoadPresets();

    }
}
