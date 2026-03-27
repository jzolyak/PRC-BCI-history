using System.Collections.Generic;
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

        private List<string>? _domainValues;
        public List<string>? DomainValues
        {
            get => _domainValues;
            set
            {
                _domainValues = value;
                OnPropertyChanged(nameof(DomainValues));
                OnPropertyChanged(nameof(HasDomain)); // UI updates automatically
            }
        }

        public bool HasDomain => DomainValues != null && DomainValues.Count > 0;

        private string? selectedDomainKey;
        public string? SelectedDomainKey
        {
            get => selectedDomainKey;
            set
            {
                selectedDomainKey = value;
                OnPropertyChanged(nameof(SelectedDomainKey));
            }
        }

        /// <summary>
        /// Returns the display value including domain for internal usage (UI binds only to Content)
        /// </summary>
        public string ValueWithDomain
        {
            get
            {
                if (!string.IsNullOrEmpty(selectedDomainKey) && !string.IsNullOrWhiteSpace(content))
                    return $"{content};domain={selectedDomainKey}";
                return content ?? string.Empty;
            }
        }
    }
}
