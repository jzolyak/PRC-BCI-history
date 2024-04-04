using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows.Controls;

namespace Starter.ValidationRules
{
    public class RegExValidationRule : ValidationRule
    {
        public string ErrorMsg { get; set; }

        public string RegExString { get; set; }

        private Regex regex;

        public RegExValidationRule(string regexString,string errorMsg)
        {
            try
            {
                ErrorMsg = errorMsg;
                RegExString = regexString;
                regex = new Regex(RegExString);
            }
            catch (Exception ex)
            {
                
            }
        }
        
        public override ValidationResult Validate(object value, CultureInfo cultureInfo)
        {

            try
            {
                if(regex == null)
                {
                    return ValidationResult.ValidResult;
                }
                if(regex.IsMatch((string)value))
                {
                    return ValidationResult.ValidResult;
                }
                else
                {
                    return new ValidationResult(false, ErrorMsg);
                }
            }
            catch (Exception e)
            {
                return new ValidationResult(false, "Internal exception");
            }
           
            return ValidationResult.ValidResult;

        }
    }
}
