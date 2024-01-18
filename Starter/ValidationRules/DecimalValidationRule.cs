using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Controls;

namespace Starter.ValidationRules
{
    public class DecimalValidationRule : ValidationRule
    {
        
        public override ValidationResult Validate(object value, CultureInfo cultureInfo)
        {

            try
            {
                if (((string)value).Length > 0)
                {
                    float v = float.Parse((string)value);
                }
            }
            catch (Exception e)
            {
                return new ValidationResult(false, "Please enter decimal or integers only");
            }
           
            return ValidationResult.ValidResult;

        }
    }
}
