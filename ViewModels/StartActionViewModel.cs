using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Input;

namespace ViewModels
{
   
        public class StartActionViewModel : PopupCloseViewModel
        {
            public ICommand StartCommand { get; set; }
            protected Action _startAction;
            public StartActionViewModel(Action startAction, Action closePopup) : base(closePopup)
            {
                StartCommand = new RelayCommand(Start);
                _startAction = startAction;
            }

            protected void Start(object obj)
            {
                if (_startAction == null) return;
                _startAction();
            }
        }
    
}
