using System;
using System.Collections.Generic;
using System.Text;

namespace ViewModels.Validation
{
    public class ValidationViewModel : ExitToMainMenuViewModel
    {
        public ValidationViewModel(Action exitAction,Action<bool> showorHideMainWindow) : base(exitAction, showorHideMainWindow)
        {

        }
    }
}
