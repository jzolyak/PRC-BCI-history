using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CoreServices.Common
{
    internal class ConfigData
    {
        public string Model { get; internal set; }
        public string Owner { get; internal set; }
        public string Category { get; internal set; }
        public object KeyValues { get; internal set; }
    }
}
