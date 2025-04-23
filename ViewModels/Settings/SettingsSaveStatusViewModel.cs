using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Input;

namespace ViewModels.Settings
{
    public class SettingsSaveStatusViewModel: PopupCloseViewModel
    {
        private string _statusMessage;
        private Action _closePopupAction;

        public string StatusMessage
        {
            get => _statusMessage;
            set { _statusMessage = value; OnPropertyChanged(nameof(StatusMessage)); }
        }

        public SettingsSaveStatusViewModel(Action closePopup) : base(closePopup)
        {
            
            //OkCommand = new RelayCommand(CheckFileName);
            //ClearCommand = new RelayCommand((x) => { FileName = String.Empty; });
        }
        public void SetStatus(string message)
        {
            StatusMessage = message;
        }
    }
}
