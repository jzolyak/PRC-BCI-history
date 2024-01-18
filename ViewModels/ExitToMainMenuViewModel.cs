using System;
using System.Collections.Generic;
using System.Text;
using System.Windows.Input;
using ViewModels.Help;

namespace ViewModels
{
    public class ExitToMainMenuViewModel:ViewModelBase
    {
        public ICommand ExitCommand { get; set; }

        protected Action<bool> _showorHideMainWindow; 
        protected Action<ChildWindowSettings> _showorHideChildWindow;

        protected Action _exitAction;
        public ExitToMainMenuViewModel(Action exitAction,Action<bool> showorHideMainWindow)
        {
            _exitAction = exitAction;
            ExitCommand = new RelayCommand(OnExit);
            _showorHideMainWindow = showorHideMainWindow;
        }
        public ExitToMainMenuViewModel(Action exitAction, Action<bool> showorHideMainWindow,Action<ChildWindowSettings> showorHideChildWindow):this(exitAction,showorHideMainWindow)
        {
            _showorHideChildWindow = showorHideChildWindow;
        }

        public void OnExit(object param)
        {
            if(_exitAction != null) _exitAction();
        }
    }
}
