using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using System.Runtime.InteropServices;

namespace BCI_Headset
{
    public class WearableSensingVR300 : IHeadset
    {
        // NOTE: Update the port string as appropriate for your use
        public static string defaultPort = "COM4";

        public static string defaultMontage = "Fz-A1A2 P3-A1A2 Pz-A1A2 P4-A1A2 PO7-A1A2 Oz-A1A2 PO8-A1A2";
        public static string defaultReference = "A1A2";

        public static string[] sourceName = new string[] { "Fz", "P3", "Pz", "P4", "PO7", "Oz", "PO8", "A1A2" };

        /* ++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++ */

        [DllImport("libDSI-Windows-x86_64.dll", EntryPoint = "DSI_Headset_New")]
        public static extern IntPtr DSI_Headset_New(string port);
        [DllImport("libDSI-Windows-x86_64.dll", EntryPoint = "DSI_Headset_Delete")]
        public static extern void DSI_Headset_Delete(IntPtr h);
        [DllImport("libDSI-Windows-x86_64.dll", EntryPoint = "DSI_Headset_Connect")]
        public static extern void DSI_Headset_Connect(IntPtr h, string port);
        [DllImport("libDSI-Windows-x86_64.dll", EntryPoint = "DSI_Headset_Disconnect")]
        public static extern void DSI_Headset_Disconnect(IntPtr h);

        [DllImport("libDSI-Windows-x86_64.dll", EntryPoint = "DSI_Error")]
        public static extern string DSI_Error();
        [DllImport("libDSI-Windows-x86_64.dll", EntryPoint = "DSI_ClearError")]
        public static extern string DSI_ClearError();
        [DllImport("libDSI-Windows-x86_64.dll", EntryPoint = "DSI_Headset_IsBlueToothInitialized")]
        public static extern bool DSI_Headset_IsBlueToothInitialized(IntPtr h);
        [DllImport("libDSI-Windows-x86_64.dll", EntryPoint = "DSI_Headset_IsConnected")]
        public static extern bool DSI_Headset_IsConnected(IntPtr h);
        [DllImport("libDSI-Windows-x86_64.dll", EntryPoint = "DSI_Headset_SetVerbosity")]
        public static extern void DSI_Headset_SetVerbosity(IntPtr h, int level);

        [DllImport("libDSI-Windows-x86_64.dll", EntryPoint = "DSI_Headset_ChooseChannels")]
        public static extern void DSI_Headset_ChooseChannels(IntPtr h, string montage, string reference, int i);
        [DllImport("libDSI-Windows-x86_64.dll", EntryPoint = "DSI_Headset_GetNumberOfChannels")]
        public static extern int DSI_Headset_GetNumberOfChannels(IntPtr h);
        [DllImport("libDSI-Windows-x86_64.dll", EntryPoint = "DSI_Headset_GetNumberOfSources")]
        public static extern int DSI_Headset_GetNumberOfSources(IntPtr h);
        [DllImport("libDSI-Windows-x86_64.dll", EntryPoint = "DSI_Headset_GetChannelByName")]
        public static extern IntPtr DSI_Headset_GetChannelByName(IntPtr h, string name);
        [DllImport("libDSI-Windows-x86_64.dll", EntryPoint = "DSI_Headset_GetSourceByName")]
        public static extern IntPtr DSI_Headset_GetSourceByName(IntPtr h, string name);
        [DllImport("libDSI-Windows-x86_64.dll", EntryPoint = "DSI_Headset_GetInfoString")]
        public static extern string DSI_Headset_GetInfoString(IntPtr h);

        [DllImport("libDSI-Windows-x86_64.dll", EntryPoint = "DSI_Headset_ConfigureBatch")]
        public static extern void DSI_Headset_ConfigureBatch(IntPtr h, int nSamples, double targetDelaySeconds);
        [DllImport("libDSI-Windows-x86_64.dll", EntryPoint = "DSI_Headset_WaitForBatch")]
        public static extern double DSI_Headset_WaitForBatch(IntPtr h);
        [DllImport("libDSI-Windows-x86_64.dll", EntryPoint = "DSI_Channel_ReadBuffered")]
        public static extern double DSI_Channel_ReadBuffered(IntPtr c);
        [DllImport("libDSI-Windows-x86_64.dll", EntryPoint = "DSI_Headset_GetNumberOfBufferedSamples")]
        public static extern int DSI_Headset_GetNumberOfBufferedSamples(IntPtr h);
        [DllImport("libDSI-Windows-x86_64.dll", EntryPoint = "DSI_Headset_SecondsSinceConnection")]
        public static extern double DSI_Headset_SecondsSinceConnection(IntPtr h);

        [DllImport("libDSI-Windows-x86_64.dll", EntryPoint = "DSI_Headset_StartBackgroundAcquisition")]
        public static extern void DSI_Headset_StartBackgroundAcquisition(IntPtr h);
        [DllImport("libDSI-Windows-x86_64.dll", EntryPoint = "DSI_Headset_StopBackgroundAcquisition")]
        public static extern void DSI_Headset_StopBackgroundAcquisition(IntPtr h);
        [DllImport("libDSI-Windows-x86_64.dll", EntryPoint = "DSI_Headset_StartDataAcquisition")]
        public static extern void DSI_Headset_StartDataAcquisition(IntPtr h);
        [DllImport("libDSI-Windows-x86_64.dll", EntryPoint = "DSI_Headset_StopDataAcquisition")]
        public static extern void DSI_Headset_StopDataAcquisition(IntPtr h);
        [DllImport("libDSI-Windows-x86_64.dll", EntryPoint = "DSI_Headset_Idle")]
        public static extern void DSI_Headset_Idle(IntPtr h, double seconds);


        [DllImport("libDSI-Windows-x86_64.dll", EntryPoint = "DSI_Headset_StartImpedanceDriver")]
        public static extern void DSI_Headset_StartImpedanceDriver(IntPtr h);
        [DllImport("libDSI-Windows-x86_64.dll", EntryPoint = "DSI_Headset_StopImpedanceDriver")]
        public static extern void DSI_Headset_StopImpedanceDriver(IntPtr h);
        [DllImport("libDSI-Windows-x86_64.dll", EntryPoint = "DSI_Source_IsReferentialEEG")]
        public static extern bool DSI_Source_IsReferentialEEG(IntPtr s);
        [DllImport("libDSI-Windows-x86_64.dll", EntryPoint = "DSI_Source_IsFactoryReference")]
        public static extern bool DSI_Source_IsFactoryReference(IntPtr s);
        [DllImport("libDSI-Windows-x86_64.dll", EntryPoint = "DSI_Source_GetImpedanceEEG")]
        public static extern double DSI_Source_GetImpedanceEEG(IntPtr s);
        [DllImport("libDSI-Windows-x86_64.dll", EntryPoint = "DSI_Headset_GetImpedanceCMF")]
        public static extern double DSI_Headset_GetImpedanceCMF(IntPtr h);

        [DllImport("libDSI-Windows-x86_64.dll", EntryPoint = "DSI_Headset_StartAnalogReset")]
        public static extern void DSI_Headset_StartAnalogReset(IntPtr h);
        [DllImport("libDSI-Windows-x86_64.dll", EntryPoint = "DSI_Headset_FlushBuffers")]
        public static extern void DSI_Headset_FlushBuffers(IntPtr h);

        /* ++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++ */

        private IntPtr handle;

        const int MAX_STATUS_VALUES = 5;    // Number of status values returned per channel, i.e.
                                            // Impedance, Avg Level, Noise, Max level, Min level

        public void CreateNew()
        {
            // Set port parameter to null to delay its setting until ConnectPort() is called
            handle = DSI_Headset_New(null);
        }

        public void Delete()
        {
            DSI_Headset_Delete(handle);
        }

        public bool ConnectPort(string port)
        {
            bool portConnected;

            DSI_Headset_Connect(handle, port);

            if (DSI_Error() != null)
            {
                portConnected = false;
            }
            else
            {
                portConnected = true;
            }
            return portConnected;
        }

        public void Disconnect()
        {
            DSI_Headset_Disconnect(handle);
        }

        public string GetDefaultHeadsetMontage()
        {
            return defaultMontage;
        }

        public string GetDefaultHeadsetReference()
        {
            return defaultReference;
        }

        public string GetDefaultHeadsetComPort()
        {
            return defaultPort;
        }

        public void SetDefaultHeadsetComPort(string port)
        {
            defaultPort = port;
        }

        /* +++++++++++++++++++++ */

        public bool IsConnected()
        {
            bool result = DSI_Headset_IsConnected(handle);
            return result;
        }

        public bool IsBlueToothInitialized()
        {
            bool result = DSI_Headset_IsBlueToothInitialized(handle);
            return result;
        }

        public string CheckError()
        {
            string error = DSI_Error();
            return error;
        }

        public string ClearError()
        {
            string error = DSI_ClearError();
            return error;
        }

        public void SetMessageVerbosity(int level)
        {
            DSI_Headset_SetVerbosity(handle, level);
        }

        /* +++++++++++++++++++++ */

        public void ChooseChannels(string montage, string reference)
        {
            // Autoswap param is set to 1 so that new input parameters overide default values
            DSI_Headset_ChooseChannels(handle, montage, reference, 1);
        }

        public int GetNumberOfChannels()
        {
            int numberOfChannels = DSI_Headset_GetNumberOfChannels(handle);
            return numberOfChannels;
        }

        public int GetNumberOfSources()
        {
            int numberOfSources = DSI_Headset_GetNumberOfSources(handle);
            return numberOfSources;
        }

        public IntPtr GetChannel(int index)
        {
            string name = sourceName[index];
            IntPtr channel = DSI_Headset_GetChannelByName(handle, name);
            return channel;
        }

        public IntPtr GetSource(int index)
        {
            string name = sourceName[index];
            IntPtr source = DSI_Headset_GetSourceByName(handle, name);
            return source;
        }

        public int GetNumberofStatusValues()
        {
            return MAX_STATUS_VALUES;
        }

        public string GetInfoString()
        {
            string info = DSI_Headset_GetInfoString(handle);
            return info;
        }

        /* +++++++++++++++++++++ */

        public void ConfigureBatch(int nSamples, double targetDelaySeconds)
        {
            DSI_Headset_ConfigureBatch(handle, nSamples, targetDelaySeconds);
        }

        public double WaitForBatch()
        {
            double timerValue = DSI_Headset_WaitForBatch(handle);
            return timerValue;
        }

        public double ReadBuffered(IntPtr channel)
        {
            double sample = DSI_Channel_ReadBuffered(channel);
            return sample;
        }

        public int GetNumberOfBufferedSamples()
        {
            int numSamples = DSI_Headset_GetNumberOfBufferedSamples(handle);
            return numSamples;
        }

        public double SecondsSinceConnection()
        {
            double timeConnected = DSI_Headset_SecondsSinceConnection(handle);
            return timeConnected;
        }

        /* +++++++++++++++++++++ */

        public void StartBackgroundAcquisition()
        {
            DSI_Headset_StartBackgroundAcquisition(handle);
        }

        public void StopBackgroundAcquisition()
        {
            DSI_Headset_StopBackgroundAcquisition(handle);
        }

        public void StartDataAcquisition()
        {
            DSI_Headset_StartDataAcquisition(handle);
        }

        public void StopDataAcquisition()
        {
            DSI_Headset_StopDataAcquisition(handle);
        }

        public void Idle(double idleTimeInSec)
        {
            DSI_Headset_Idle(handle, idleTimeInSec);
        }

        /* +++++++++++++++++++++ */

        public void StartImpedanceDriver()
        {
            DSI_Headset_StartImpedanceDriver(handle);
        }

        public void StopImpedanceDriver()
        {
            DSI_Headset_StopImpedanceDriver(handle);
        }

        public bool IsReferentialEEG(IntPtr source)
        {
            bool testFlag = DSI_Source_IsReferentialEEG(source);
            return testFlag;
        }

        public bool IsFactoryReference(IntPtr source)
        {
            bool testFlag = DSI_Source_IsFactoryReference(source);
            return testFlag;
        }

        public double GetImpedanceEEG(IntPtr source)
        {
            double impedanceValue = DSI_Source_GetImpedanceEEG(source);
            return impedanceValue;
        }

        public double GetImpedanceCMF()
        {
            double impedanceValue = DSI_Headset_GetImpedanceCMF(handle);
            return impedanceValue;
        }

        /* +++++++++++++++++++++ */

        public void StartAnalogReset()
        {
            DSI_Headset_StartAnalogReset(handle);
        }

        public void FlushBuffers()
        {
            DSI_Headset_FlushBuffers(handle);
        }

        /* +++++++++++++++++++++ */

    }
}
