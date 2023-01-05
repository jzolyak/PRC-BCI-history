using BCI_Headset;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CoreServices.Headset
{
    public class HeadsetService
    {
        private WearableSensingVR300 headset;

        public WearableSensingVR300 Headset { get { return headset; } }
        public HeadsetService()
        {
            headset = new WearableSensingVR300();
        }

        public bool CheckConnection()
        {
            return headset.IsConnected();
        }
        public async Task ConnectAsync()
        {
            const int samplesPerBatch = 9;              // 9 samples = 30 msec @ Fs=300 Hz
            const double bufferAheadSec = 0.100;
            headset.CreateNew();
            headset.SetMessageVerbosity(2);

            string port = headset.GetDefaultHeadsetComPort();
            var passTest = headset.ConnectPort(port);

            if (passTest == true)
            {
                Logger.Info(this, "\nConnection made to headset...\n");
            }
            else
            {
                Logger.Info(this, "\nFailed connecting to headset...\n");
            }

            string montage = headset.GetDefaultHeadsetMontage();
            string reference = headset.GetDefaultHeadsetReference();
            headset.ChooseChannels(montage, reference);

            //string info = headset.GetInfoString();
            //Console.WriteLine(info);

            headset.StartAnalogReset();
            await Task.Delay(100);

            headset.ConfigureBatch(samplesPerBatch, bufferAheadSec);
        }

         
    }
}
