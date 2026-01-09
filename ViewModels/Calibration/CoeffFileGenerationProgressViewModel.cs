using CoreServices.Models;
using System;
using System.Windows.Input;

namespace ViewModels.Calibration
{
    public class CoeffFileGenerationProgressViewModel : ViewModelBase
    {
        private string _statusMessage;
        private bool _isInProgress;
        private bool _isOkVisible;

        public string StatusMessage
        {
            get => _statusMessage;
            set { _statusMessage = value; OnPropertyChanged(nameof(StatusMessage)); }
        }

        public bool IsInProgress
        {
            get => _isInProgress;
            set { _isInProgress = value; OnPropertyChanged(nameof(IsInProgress)); }
        }

        public bool IsOkVisible
        {
            get => _isOkVisible;
            set { _isOkVisible = value; OnPropertyChanged(nameof(IsOkVisible)); }
        }

        public ICommand OkCommand { get; }

        public CoeffFileGenerationProgressViewModel()
        {
            OkCommand = new RelayCommand(_ => Environment.Exit(0));
        }

        // ---------------- STATE HELPERS ----------------

        public void SetAttemptInProgress(int attempt)
        {
            StatusMessage =
                $"Generating Coefficient file.\nAttempt {attempt} of 3. Please wait...";
            IsInProgress = true;
            IsOkVisible = false;
        }

        public void SetSuccess()
        {
            StatusMessage = "Generating Coefficient file. Completed successfully.";
            IsInProgress = false;
            IsOkVisible = true;
        }

        public void SetFailure()
        {
            StatusMessage = "Generating Coefficient file. Failed!";
            IsInProgress = false;
            IsOkVisible = true;
        }
    }
}
