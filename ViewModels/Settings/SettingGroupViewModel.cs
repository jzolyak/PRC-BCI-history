using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Input;

namespace ViewModels.Settings
{
    public class SettingGroupViewModel : ViewModelBase
    {
        public ICommand OpenCmd { get; set; }
        public string DisplayName { get; set; }
        public string Name { get; set; }

        private Action<string> _openCommandAction;

        public SettingGroupViewModel(Action<string> openCommandAction, string displayName, string name)
        {
            this.DisplayName = displayName;
            this.Name = name;
            this._openCommandAction = openCommandAction;
            OpenCmd = new RelayCommand(Open);
        }

        private void Open(object obj)
        {
            if (obj == null || string.IsNullOrEmpty(obj.ToString()) || _openCommandAction == null) return;
            _openCommandAction(obj.ToString());
        }

    }
}
