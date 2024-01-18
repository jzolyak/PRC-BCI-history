using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ViewModels.Help
{
    public class ViewModelHelper
    {

        public static Rule findRule(string name)
        {
            if(string.IsNullOrEmpty(name)) return null;
            if(name.ToLower().Equals("numberonly"))
            {
                return new Rule() { ErrorMsg="Please enter numeric digits only",RegExString = "^[0-9]+$" };
            }
            else if(name.ToLower().Equals("decimalonly"))
            {
                return new Rule() { ErrorMsg = "Please enter decimal value", RegExString = @"^(-?[1-9]+\d*([.]\d+)?)$|^(-?0[.]\d*[1-9]+)$|^0$|^0.0$" };

            }
            else 
            {
                return new Rule() { ErrorMsg = "Please enter a string value", RegExString = ".*" };
            }
        }

        public static string getKey(string key, string header)
        {
            if (string.IsNullOrEmpty(key) || string.IsNullOrEmpty(header)) return string.Empty;
            string[] keyArray = key.Split(';');
            if(keyArray.Length <2 || !keyArray[0].ToLower().Equals(header.ToLower())) return string.Empty;
            return keyArray[1];
        }

        public static string getRuleName(string key)
        {
            if (string.IsNullOrEmpty(key)) return string.Empty;
            string[] keyArray = key.Split(';');
            if(keyArray.Length < 3) return string.Empty;
            return keyArray[2];
        }

        public static Rule getRule(string key)
        {
            var rulename = getRuleName(key);
            if(string.IsNullOrEmpty(rulename)) return null;
            return findRule(rulename);
        }
    }
}
