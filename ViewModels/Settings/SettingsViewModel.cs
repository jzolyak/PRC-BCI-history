using System;
using System.Collections.Generic;
using System.Text;
using System.Windows.Input;

namespace ViewModels.Settings
{
    public class SettingsViewModel : ExitToMainMenuViewModel
    {

        private Action<string> _openCommandAction;
        public ICommand OpenCmd { get; set; }


        public SettingsViewModel(Action<string> openCommandAction,Action exitAction,Action<bool> showorHideMainWindow) : base(exitAction, showorHideMainWindow)
        {
            _openCommandAction = openCommandAction;
            OpenCmd = new RelayCommand(Open);
        }

        private void Open(object obj)
        {
            if (obj == null || string.IsNullOrEmpty(obj.ToString()) || _openCommandAction == null) return;
            _openCommandAction(obj.ToString());
        }
    }
}
