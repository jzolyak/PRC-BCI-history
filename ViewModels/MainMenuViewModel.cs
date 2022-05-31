using System;
using System.Collections.Generic;
using System.Text;
using System.Windows.Input;
using ViewModels.Connectivity;

namespace ViewModels
{
    public class MainMenuViewModel:ViewModelBase
    {
        public HeadsetConnectionStatusViewModel HeadsetConnectionStatusViewModel { get; set; }
        public ICommand OpenCmd { get; set; }
        public ICommand CancelCmd { get; set; }
        public ICommand FinishCmd { get; set; }
        private Action<string> _openCommandAction;
        private Action _closeCommandAction;
      

        public MainMenuViewModel(Action<string> openCommandAction,Action closeAction)
        {
            _openCommandAction= openCommandAction;
            _closeCommandAction= closeAction;
            OpenCmd = new RelayCommand(Open);
            CancelCmd = new RelayCommand(Close);
            FinishCmd = new RelayCommand(Close);
        }

        private void Close(object obj)
        {
            if (_closeCommandAction == null) return;
            _closeCommandAction();
        }

        private void Open(object obj)
        {
            if (obj == null || string.IsNullOrEmpty(obj.ToString()) || _openCommandAction==null) return;
            _openCommandAction(obj.ToString());
        }
    }
}
