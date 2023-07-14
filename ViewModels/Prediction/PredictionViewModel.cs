using System;
using System.Collections.Generic;
using System.Text;

namespace ViewModels.Prediction
{
    public class PredictionViewModel : ExitToMainMenuViewModel
    {
        public PredictionViewModel(Action exitAction,Action<bool> showorHideMainWindow) : base(exitAction,showorHideMainWindow)
        {
        }
    }
}
