using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Input;

namespace ViewModels.Startup
{
    public class HeadsetStartupStatusViewModel:ViewModelBase        
    {
        private bool _enableNext;
        private string _connectionResult;
        public string ConnectionResult { get { return _connectionResult; } set { _connectionResult = value; OnPropertyChanged(nameof(ConnectionResult)); } }
        public bool EnableNext { get { return _enableNext; } set { _enableNext = value; OnPropertyChanged(nameof(EnableNext)); } }
        public ICommand NextCmd { get; set; }
        public ICommand CancelCmd { get; set; }
        private Action _closeAction;
        private Action _nextAction;


        public HeadsetStartupStatusViewModel(Action nextAction,Action closeAction)
        {
            _nextAction = nextAction;
            _closeAction = closeAction;
            CancelCmd = new RelayCommand(Cancel);
            NextCmd = new RelayCommand(Next);
        }

        public void UpdateStatus(string statusText, bool enableNext)
        {
            ConnectionResult = statusText;
            EnableNext = enableNext;    
        }

        private void Cancel(object obj)
        {
            if (_closeAction != null) _closeAction();
        }
        private void Next(object obj)
        {
            if (_nextAction != null) _nextAction();
        }
    }
}
