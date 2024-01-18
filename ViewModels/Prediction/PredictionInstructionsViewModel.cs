using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ViewModels.Prediction
{
    public class PredictionInstructionsViewModel : StartActionViewModel
    {
        public PredictionInstructionsViewModel(Action startAction, Action closePopup) : base(startAction, closePopup)
        {
        }
    }
}
