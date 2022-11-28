using CoreServices.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Timers;
using System.Windows.Input;

namespace ViewModels.Calibration
{

    public class CountDownViewModel:ViewModelBase
    {
        private string _text;
        private Action _closeAction;
        private Timer _timer;
        private int _counter;
        public string Text { get { return _text; } set { _text = value; OnPropertyChanged(nameof(Text)); } }
        public ICommand OkCommand { get; set; }
        private Action<UIInstruction> _resultAction;
        public CountDownViewModel(Action closeAction, Action<UIInstruction> resultAction)
        {
            OkCommand = new RelayCommand(Ok);
            _closeAction = closeAction;           
            _timer= new Timer(1000);
            _timer.Elapsed += _timer_Elapsed;
            _resultAction = resultAction;   
        }

        public void Reset()
        {
            _timer.Stop();  
        }

        public void Start()
        {
            _counter = 5;
            Text = _counter.ToString();
            _timer.Start();
        }
        private void _timer_Elapsed(object? sender, ElapsedEventArgs e)
        {
            if(_counter == 0)
            {
                Ok(null);
                Reset();
                return;
            }
            Text = _counter.ToString();
            _counter--;
        }

        private void Ok(object obj)
        {
            if(_closeAction != null) _closeAction();
            _resultAction(new UIInstruction { Type = UIType.CountDownWindow, Result = true });
        }
    }
}
