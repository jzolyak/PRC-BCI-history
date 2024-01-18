using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ViewModels.Help
{

    public class ChildWindowSettings
    {
        public string WindowName { get; set; }
        public bool ShowWindow { get; set; }
        public object DataContext { get; set; }
    }

}
