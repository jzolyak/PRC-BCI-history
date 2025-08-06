using System.Collections.Generic;
using System.ComponentModel;
using ViewModels.Help;

namespace ViewModels.Settings
{
    public class SettingsLineItemViewModel : ViewModelBase
    {
        private string header;
        public string Header
        {
            get => header;
            set { header = value; OnPropertyChanged(nameof(Header)); }
        }

        private string content;
        public string Content
        {
            get => content;
            set { content = value; OnPropertyChanged(nameof(Content)); }
        }

        private List<string> _domainValues;
        public List<string> DomainValues
        {
            get => _domainValues;
            set
            {
                _domainValues = value;
                OnPropertyChanged(nameof(DomainValues));
                OnPropertyChanged(nameof(HasDomain)); // Ensure UI updates!
            }
        }

        public bool HasDomain => DomainValues != null && DomainValues.Count > 0;

        public string? SelectedDomainKey { get; set; }
    }
}
