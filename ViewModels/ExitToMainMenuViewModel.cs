using System;
using System.Collections.Generic;
using System.Text;
using System.Windows.Input;

namespace ViewModels
{
    public class ExitToMainMenuViewModel:ViewModelBase
    {
        public ICommand ExitCommand { get; set; }

        private Action _exitAction;
        public ExitToMainMenuViewModel(Action exitAction)
        {
            _exitAction = exitAction;
            ExitCommand = new RelayCommand(OnExit);
        }

        public void OnExit(object param)
        {
            if(_exitAction != null) _exitAction();
        }
    }
}
