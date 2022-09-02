using System;
using System.Collections.Generic;
using System.Text;
using System.Windows.Input;

namespace ViewModels
{
    public class PopupCloseViewModel:ViewModelBase
    {
        public ICommand PopupCloseCommand { get; set; }
        private Action popupcloseAction;
        public PopupCloseViewModel(Action popUpClose)
        {
            PopupCloseCommand = new RelayCommand(PopupClose);
            popupcloseAction = popUpClose;
        }

        private void PopupClose(object obj)
        {
            if (popupcloseAction == null) return;
            popupcloseAction();
        }
    }
}
