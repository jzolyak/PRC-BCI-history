using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using ViewModels.Help;

namespace ViewModels.Settings
{
    public class SettingsLineItemViewModel:ViewModelBase
    {
        private string header;
        public string Header { get { return header; } set { header = value;OnPropertyChanged(nameof(header)); } }

        private string content;
        public string Content { get { return content; } set { content = value;OnPropertyChanged(nameof(content)); } }
        private List<Rule> rules;
        public List<Rule> Rules { get { return rules; } set { rules = value; OnPropertyChanged(nameof(Rules)); } }
 
    }
}
