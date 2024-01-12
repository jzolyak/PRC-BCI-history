using CoreServices.Common;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Input;

namespace ViewModels.Startup
{
    public class FirstScreenViewModel:ViewModelBase
    {
        private const string portKey = "HeadSetPort";

        private string _comport;
        public string Comport { get { return _comport; } set { _comport = value; OnPropertyChanged(nameof(Comport)); } }

        private Action _nextAction;
        private Action _closeAction;
        private SettingsService _userSettingsService;
        public ICommand CancelCmd { get;set; }
        public ICommand NextCmd { get;set; }    

        public FirstScreenViewModel(Action nextAction,Action closeAction)
        {
            _nextAction = nextAction;
            _closeAction= closeAction;
            CancelCmd = new RelayCommand(Cancel);
            NextCmd = new RelayCommand(Next);
            _userSettingsService = ServiceLocator.UserSettingsService;
            _comport = _userSettingsService.GetSettingsValue(portKey);
            if (_comport == null)
            {
                _comport = "COM4";
            }
        }

        private void Cancel(object obj)
        {
            if (_closeAction != null) _closeAction();
        }

        private void Next(object obj)
        {
            _userSettingsService.SaveUserSettings(portKey, _comport);
            if (_nextAction != null) _nextAction();
        }

    }
}
