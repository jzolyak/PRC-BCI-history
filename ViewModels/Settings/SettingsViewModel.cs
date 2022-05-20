using System;
using System.Collections.Generic;
using System.Text;

namespace ViewModels.Settings
{
    internal class SettingsViewModel : ExitToMainMenuViewModel
    {
        public SettingsViewModel(Action exitAction) : base(exitAction)
        {
        }
    }
}
