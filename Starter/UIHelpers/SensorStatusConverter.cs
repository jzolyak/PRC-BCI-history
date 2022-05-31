using CoreServices.Models;
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
    public class SensorStatusConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if(value == null || string.IsNullOrEmpty(value.ToString()) || parameter == null || string.IsNullOrEmpty(parameter.ToString())) return null;
            string? sensorStatus = value.ToString();
            string? controlName = parameter.ToString();
            if(sensorStatus.ToUpper().Equals(Constants.Sensor_Status_Green.ToUpper()) && controlName.ToUpper().Equals("GREEN"))
            {
                return Visibility.Visible;
            }
            if (sensorStatus.ToUpper().Equals(Constants.Sensor_Status_Yellow.ToUpper()) && controlName.ToUpper().Equals("YELLOW"))
            {
                return Visibility.Visible;
            }

            if (sensorStatus.ToUpper().Equals(Constants.Sensor_Status_Red.ToUpper()) && controlName.ToUpper().Equals("RED"))
            {
                return Visibility.Visible;
            }
            return Visibility.Collapsed;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            return null;
        }
    }
}
