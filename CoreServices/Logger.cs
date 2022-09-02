using System;
using System.Collections.Generic;
using System.Text;
[assembly: log4net.Config.XmlConfigurator(Watch = true)]
namespace CoreServices
{
    
    public class Logger
    {
        private static readonly log4net.ILog log = log4net.LogManager.GetLogger(System.Reflection.MethodBase.GetCurrentMethod().DeclaringType);
        static Logger()
        {
            
        }
        public static void Info(Object sender,string message)
        {
            if (sender == null) return;
            log.Info(sender.GetType().FullName + ": " + message);
        }

        public static void Error(Object sender, string message)
        {
            if (sender == null) return;
            log.Error(sender.GetType().FullName + ": " + message);
        }

        public static void Debug(Object sender, string message)
        {
            if (sender == null) return;
            log.Debug(sender.GetType().FullName + ": " + message);
        }
    }
}
