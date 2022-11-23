using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BCI_Headset
{
    public interface IHeadset
    {
        // Create handle to DLL instance
        void CreateNew();

        void Delete();

        // Establish communication channel with headset via specified COM port
        bool ConnectPort(string port);

        void Disconnect();

        string GetDefaultHeadsetMontage();

        string GetDefaultHeadsetReference();

        string GetDefaultHeadsetComPort();

        /* +++++++++++++++++++++ */

        bool IsConnected();

        bool IsBlueToothInitialized();

        string CheckError();

        string ClearError();

        void SetMessageVerbosity(int level);

        /* +++++++++++++++++++++ */

        void ChooseChannels(string montage, string reference);

        int GetNumberOfChannels();

        int GetNumberOfSources();

        IntPtr GetChannel(int index);

        IntPtr GetSource(int index);

        int GetNumberofStatusValues();

        string GetInfoString();

        /* +++++++++++++++++++++ */

        void ConfigureBatch(int nSamples, double targetDelaySeconds);

        double WaitForBatch();

        double ReadBuffered(IntPtr channel);

        int GetNumberOfBufferedSamples();

        double SecondsSinceConnection();

        /* +++++++++++++++++++++ */

        void StartBackgroundAcquisition();

        void StopBackgroundAcquisition();

        void StartDataAcquisition();

        void StopDataAcquisition();

        void Idle(double idleTimeInSec);

        /* +++++++++++++++++++++ */

        void StartImpedanceDriver();

        void StopImpedanceDriver();

        bool IsReferentialEEG(IntPtr source);

        bool IsFactoryReference(IntPtr source);

        double GetImpedanceEEG(IntPtr source);

        double GetImpedanceCMF();

        /* +++++++++++++++++++++ */

        void StartAnalogReset();

        void FlushBuffers();

        /* +++++++++++++++++++++ */

    }
}
