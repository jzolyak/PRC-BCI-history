using System;
using System.Collections.Generic;
using System.Text;

namespace ViewModels.Settings
{
    internal class SettingsViewModel : ExitToMainMenuViewModel
    {
        public SettingsViewModel(Action exitAction,Action<bool> showorHideMainWindow) : base(exitAction, showorHideMainWindow)
        {
        }
    }
}
