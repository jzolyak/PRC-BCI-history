using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Data;

namespace Starter.UIHelpers
{
    public class BooltoVisibilityConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            try
            {
                if (value == null) return Visibility.Collapsed;
                bool boolValue = (bool)value;
                if(boolValue)
                {
                    return Visibility.Visible;
                }
                return Visibility.Collapsed;
            }
            catch (Exception ex)
            {
                return Visibility.Collapsed;
            }
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            return null;
        }
    }
   
}
