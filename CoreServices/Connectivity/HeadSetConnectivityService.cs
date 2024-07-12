using CoreServices.Models;
using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Threading.Tasks.Dataflow;
using System.IO;

using BCI_Headset;

namespace CoreServices.Connectivity
{
    public class HeadSetConnectivityService
    {

        /* ++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++ */

        readonly double[] FIR_LPF_100Hz_coeff = new double[]
        {
             0.0,                 4.933079467458e-20,-4.465650265995e-05,0.0001029276657803,
            -6.359684207672e-19, -0.0002997678922299,0.0004418484107405,-6.964610729674e-18,
            -0.0008226146835013,  0.001065285506596,-3.805835296595e-18, -0.00166611933089,
             0.002029164266573,  -6.204622366953e-18,-0.002894533385478, 0.003403452046381,
            -8.978587905888e-18, -0.004593746128194, 0.005285034889751,-1.197444213609e-17,
            -0.006891626243364,   0.007823512638434,-1.502663367585e-17, -0.01000052286977,
             0.0112765698393,    -1.796649793474e-17, -0.01431479869372,  0.01614229394474,
            -2.06315775458e-17,  -0.02066796264898,   0.0235294837869,-2.28745997854e-17,
            -0.03116767248281,    0.03647915307478,-2.457161488751e-17,  -0.0530346347013,
             0.06722976144993,   -2.562884552739e-17,  -0.1369840513167,   0.2752401580597,
             0.6666681226001,     0.2752401580597,  -0.1369840513167,-2.562884552739e-17,
             0.06722976144993,   -0.0530346347013,-2.457161488751e-17,  0.03647915307478,
            -0.03116767248281,   -2.28745997854e-17,   0.0235294837869, -0.02066796264898,
            -2.06315775458e-17,   0.01614229394474, -0.01431479869372,-1.796649793474e-17,
             0.0112765698393,    -0.01000052286977,-1.502663367585e-17, 0.007823512638434,
            -0.006891626243364,  -1.197444213609e-17, 0.005285034889751,-0.004593746128194,
            -8.978587905888e-18,  0.003403452046381,-0.002894533385478,-6.204622366953e-18,
             0.002029164266573,  -0.00166611933089,-3.805835296595e-18, 0.001065285506596,
            -0.0008226146835013, -6.964610729674e-18,0.0004418484107405,-0.0002997678922299,
            -6.359684207672e-19,  0.0001029276657803,-4.465650265995e-05,4.933079467458e-20,
             0.0
        };

        /* ++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++ */

        bool exitFlag = false;
        bool resetFlag = false;
        bool continueFlag = false;
        bool waitToContinue = false;
        bool lockoutStart = false;

        private List<SensorMeasurements> _measurements;

        public event EventHandler<EventArgs<List<SensorMeasurements>>>? RaiseMeasurementsChange =null;

        public event EventHandler<EventArgs<string>>? RaiseRecommendationChange = null;

        public event EventHandler<EventArgs<int[]>>? RaiseSensorStatusColorChange = null;
        public string _currentRecommandation = string.Empty;
        public HeadSetConnectivityService()
        {
            _measurements = new List<SensorMeasurements>();
            _measurements.Add(new SensorMeasurements() { Sensor = "Fz", Impedance = 0.0, Level = 0.0, Noise = 0.0 });
            _measurements.Add(new SensorMeasurements() { Sensor = "P3", Impedance = 0.0, Level = 0.0, Noise = 0.0 });
            _measurements.Add(new SensorMeasurements() { Sensor = "Pz", Impedance = 0.0, Level = 0.0, Noise = 0.0 });
            _measurements.Add(new SensorMeasurements() { Sensor = "P4", Impedance = 0.0, Level = 0.0, Noise = 0.0 });
            _measurements.Add(new SensorMeasurements() { Sensor = "PO7", Impedance = 0.0, Level = 0.0, Noise = 0.0 });
            _measurements.Add(new SensorMeasurements() { Sensor = "Oz", Impedance = 0.0, Level = 0.0, Noise = 0.0 });
            _measurements.Add(new SensorMeasurements() { Sensor = "PO8", Impedance = 0.0, Level = 0.0, Noise = 0.0 });
            _measurements.Add(new SensorMeasurements() { Sensor = "Ear", Impedance = 0.0, Level = 0.0, Noise = 0.0 });
        }

        public List<SensorMeasurements> GetCurrentSensorMeasurements()
        {
            return _measurements;
        }

        protected virtual void OnMeasurementsChange(List<SensorMeasurements>? newSensorMeasurements)
        {
            EventHandler<EventArgs<List<SensorMeasurements>>>? raiseMeasurementsChange = RaiseMeasurementsChange;
            if (raiseMeasurementsChange != null)
            {
                raiseMeasurementsChange(this, new EventArgs<List<SensorMeasurements>>(newSensorMeasurements));
            }
        }

        public void ClearRecommendation()
        {
            _currentRecommandation = string.Empty;
        }
        protected virtual void OnRecommendationChange(string recommendation)
        {
            _currentRecommandation += recommendation;
            EventHandler<EventArgs<string>>? raiseRecommendationChange = RaiseRecommendationChange;
            if (raiseRecommendationChange != null)
            {
                raiseRecommendationChange(this, new EventArgs<string>(_currentRecommandation));
            }
        }

        protected virtual void OnSensorStatusColorChange(int[] statusColors)
        {
            EventHandler<EventArgs<int[]>>? raiseSensorStatusColorChange = RaiseSensorStatusColorChange;
            if (raiseSensorStatusColorChange != null)
            {
                raiseSensorStatusColorChange(this, new EventArgs<int[]>(statusColors));
            }
        }

        public void Continue()
        {
            continueFlag = true;
        }

        public void Reset()
        {
            resetFlag = true;
        }
        public void Exit()
        {
            exitFlag = true;
        }

        /* ++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++ */
        /* ++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++ */

        public async Task ConnectivityMode()
        {
            BinaryWriter bw;

            bool passTest = true;
            bool goodMeasurment = false;
            bool pauseForReset = false;

            const int samplesPerBatch = 15;         // 15 samples = 50 msec @ Fs=300 Hz

            int goodResultCount = 0;
            int oneSecondCounter = 0;
            int failThreshold = 5;                  // Number of 1 sec measurments in 10 sec interval that
                                                    // failed good range, hnece triggering sensor adjustment

            const double MEASURE = 10.0;
            const double WAITING = 20.0;
            const double RESET = 30.0;

            double stateMarker = MEASURE;

            double bufferAheadSec = 0.06;           // 60 msec


            /* +++++++++++++++++++++++++++++++++++++++ */

            // For now just create a dummy binary file to store the status results
            try
            {
                string path = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData) + "\\BCI";
                bw = new BinaryWriter(new FileStream(path+"\\SensorData.bin", FileMode.Create));
            }
            catch (IOException e)
            {
                Logger.Error(this,e.Message + "\n Cannot create file.");
                return;
            }

            /* +++++++++++++++++++++++++++++++++++++++ */

           Logger.Info(this,"Starting Connectivity Mode ...");
            Logger.Info(this, "Starting Connectivity Mode ...");

            WearableSensingVR300 headset = new WearableSensingVR300();

            headset.CreateNew();
            headset.SetMessageVerbosity(2);

            string port = headset.GetDefaultHeadsetComPort();
            passTest = headset.ConnectPort(port);
            await Task.Delay(1000);

            if (passTest == true)
            {
               OnRecommendationChange("Connection to COM port passed ... \n");
            }
            else
            {
                OnRecommendationChange("Connection to COM port failed ... \n");
            }

            /*
            passTest = headset.IsBlueToothInitialized();
            await Task.Delay(1000);

            if (passTest == true)
            {
                OnRecommendationChange("Bluetooth Initialization passed ... \n");
            }
            else
            {
                OnRecommendationChange("Bluetooth Initialization failed ... \n");
            }
            */

            passTest = headset.IsConnected();
            await Task.Delay(1000);

            if (passTest == true)
            {
                OnRecommendationChange("BCI program is connected to headset ... \n");
            }
            else
            {
                OnRecommendationChange("BCI program is not connected to headset ... \n");
            }

            /* +++++++++++++++++++++++++++++++++++++++ */

            string montage = headset.GetDefaultHeadsetMontage();
            string reference = headset.GetDefaultHeadsetReference();
            headset.ChooseChannels(montage, reference);

            // Each channel is (sensor source - reference signal), thus #channels = #sources - 1
            int numberOfChannels = headset.GetNumberOfChannels();

            // Assumes no TRIG channel in montage, just sensors
            int numberOfSources = headset.GetNumberOfSources();

            // Number of status values per channel returned when measuring connectivity
            int numStatusValues = headset.GetNumberofStatusValues();

            int[] statusColorCode = new int[numberOfSources];
            int[] testColorCode = new int[numberOfSources];

            double[] statusBuffer = new double[numberOfSources * numStatusValues];

            double[,] statusTableValues = new double[numberOfSources, numStatusValues];

            SensorEvents[] sensorEventCounters = new SensorEvents[numberOfSources];

            /* +++++++++++++++++++++++++++++++++++++++ */

            headset.StartAnalogReset();
            await Task.Delay(100);

            headset.StartImpedanceDriver();

            headset.ConfigureBatch(samplesPerBatch, bufferAheadSec);

            headset.StartBackgroundAcquisition();


            /* ++++++++++++++++++++++++++++++++++++++ */
            /*    !!!   REAL WORK STARTS HERE  !!!    */
            /* ++++++++++++++++++++++++++++++++++++++ */

            CancellationTokenSource tokenSource = new CancellationTokenSource();

            var statusBufferBlock = new BufferBlock<double[]>();

            // Start separate task that is fetching sensor samples from headset
            // and measuring sensor status over 1 second intervals.
            Task t = Task.Run(() => MeasureConnectivity(headset, tokenSource.Token, statusBufferBlock));

            // The task continuously measures sensor values and reports back an average value once a second.
            // The average values are updated on the screen over a 10 second interval, and those values
            // are interpreted into a recommendation on what adjustments the user should make at specific
            // sensor locations located on the headset. 
            //
            // The GUI (main) thread waits for the user to complete the recommended adjustments, as well
            // as their decision on what to do next based which button was selected on the display, i.e.
            // CONTINUE = take another 10 seconds of measurements to determine the affect the adjustment
            // made on connectivity, RESET = perform hardware reset of sensors and restart connectivity
            // adjustments all over again from scratch, EXIT = user is done with adjustments.

            while (exitFlag == false)
            {
                statusBuffer = await UpdataSensorStatusAsync(statusBufferBlock);

                statusTableValues = UnpackStatusTableValues(numberOfSources, numStatusValues, statusBuffer);

                // User clicked on the Reset button
                if (resetFlag == true)
                {
                    stateMarker = RESET;

                    oneSecondCounter = 0;
                    resetFlag = false;
                    continueFlag = false;
                    waitToContinue = false;
                    pauseForReset = true;

                    headset.StartAnalogReset();

                    ResetSensorEventCounters(ref sensorEventCounters);
                }

                // Intermediate state. Wait 2 seconds for sensor values to settle after hardware reset.
                else if (pauseForReset == true)
                {
                    if (++oneSecondCounter >= 2)
                    {
                        pauseForReset = false;
                        oneSecondCounter = 0;
                    }
                }

                // Continue collecting and reporting one second measurements until 10 second interval is 
                // reached, then make a recommendation on what adjustment to make to the headset.
                else
                {
                    if (waitToContinue == false)
                    {
                        stateMarker = MEASURE;

                        statusColorCode = UpdateStatusColorCode(statusTableValues, ref sensorEventCounters);

                        // Check if 10 seconds has elasped
                        if (++oneSecondCounter < 10)
                        {
                            UpdateMeasurements(statusTableValues, statusColorCode);
                        }
                        else
                        {
                            // Ten seconds has elasped, time to make next recommendation.
                            oneSecondCounter = 0;

                            goodMeasurment = MakeSensorRecommendation(failThreshold, sensorEventCounters, testColorCode);

                            UpdateMeasurements(statusTableValues, testColorCode);

                            ResetSensorEventCounters(ref sensorEventCounters);

                            // Inform user what to do next
                            if (goodMeasurment == true)
                            {
                                waitToContinue = true;

                                if (++goodResultCount == 3)
                                {
                                    OnRecommendationChange("\nConnectivity Test Successfully Completed! Click EXIT to end testing");
                                }
                                else
                                {
                                    OnRecommendationChange("\nTest measurement is good, click CONTINUE to run next test");
                                }
                            }

                            else  // (goodMeasurment == false)
                            {
                                goodResultCount = 0;
                                waitToContinue = true;

                                // Display test failed message
                                OnRecommendationChange("\nSensor measurement are out of range.");

                                // Display adjustment instructions again
                                OnRecommendationChange("\nPlease adjust each sensor as follows:");
                                OnRecommendationChange("\nRED:    Twist tool in hair, then press down on sensor");
                                OnRecommendationChange("\nYELLOW: Press down on sensor for 2 sec, no twist");
                                OnRecommendationChange("\nGREEN:  Good reading");

                                // Display what to do after adjustments are made
                                OnRecommendationChange("\n\nPress CONTINUE button after sensor adjustments are made ... \n");
                            }
                        }
                    }

                    else // (waitToContinue == true)
                    {
                        stateMarker = WAITING;

                        // User has made adjustments, if needed, and ready to take another 10 second measurment
                        if (continueFlag == true)
                        {
                            oneSecondCounter = 0;
                            continueFlag = false;
                            waitToContinue = false;

                            ResetSensorEventCounters(ref sensorEventCounters);
                        }
                    }

                }  // if (resetFlag == true)

                // Store copy of sensor status information once a second to binary file.
                RecordSensorData(bw, statusTableValues, stateMarker);

            }  // while (exitFlag == false)

            // Stop task that is fetching sensor samples from headset
            tokenSource.Cancel();

            /* +++++++++++++++++++++++++++++++++++++++ */

            headset.StopBackgroundAcquisition();

            headset.StopImpedanceDriver();
            await Task.Delay(1000);

            headset.Idle(1.0);
            headset.Disconnect();
            await Task.Delay(1000);

            headset.Delete();

           Logger.Info(this,"Terminating the application...");

        }  // ConnectivityMode()

        /* ++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++ */
        /* ++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++ */

        public void MeasureConnectivity(IHeadset headset, CancellationToken cts, ITargetBlock<double[]> bufferBlock)
        {
            const int CIRCULAR_BUF_LEN = 512;       // Num sensor samples per chan in the circular buffer

            const int samplesPerBatch = 15;         // 15 samples = 50 msec @ Fs=300 Hz

            int batchSize;                  // batchCounter * samplesPerBatch
            int batchCounter = 0;           // Track # of batches in 1.0 second

            int channelIndex;
            int sampleIndex;
            int sourceIndex;

            int bufferIndex;
            int circBufReadIndex = 0;
            int circBufWriteIndex = 0;

            int filterIndex;
            int filterBufIndex = 0;
            int startIntervalIndex = 0;

            int filterState = 0;
            int ntaps = FIR_LPF_100Hz_coeff.Length;

            double t0;

            double signalValue;
            double averageValue;
            double accum;
            double difference;
            double variance;
            double standardDeviation;

            /* +++++++++++++++++++++++++++++++++++++++ */

            int numberOfChannels = headset.GetNumberOfChannels();
            int numberOfSources = headset.GetNumberOfSources();
            int numStatusValues = headset.GetNumberofStatusValues();

            double[] minValue = new double[numberOfChannels];
            double[] maxValue = new double[numberOfChannels];
            double[] accumSum = new double[numberOfChannels];

            double[,] CircularBuffer = new double[numberOfChannels, CIRCULAR_BUF_LEN];

            double[] ImpedanceBuffer = new double[numberOfSources];

            double[] minImpedance = new double[numberOfSources];
            double[] maxImpedance = new double[numberOfSources];
            double[] sumImpedance = new double[numberOfSources];

            double[] statusBuffer = new double[numberOfSources * numStatusValues];

            double[,] statusTableValues = new double[numberOfSources, numStatusValues];

            double[] accumFilteredSum = new double[numberOfChannels];
            double[,] FilterOutputBuffer = new double[numberOfChannels, CIRCULAR_BUF_LEN];
            double[,] delayedInputBuffer = new double[numberOfChannels, ntaps * 2];

            /* +++++++++++++++++++++++++++++++++++++++ */


            while (!cts.IsCancellationRequested)
            {
                // This blocking call aims to ensure that batches of samples are picked
                // up at exactly regular intervals (t0 timestamps should be very regular)
                t0 = headset.WaitForBatch();

                batchCounter++;

                for (channelIndex = 0; channelIndex < numberOfChannels; channelIndex++)
                {
                    IntPtr c = headset.GetChannel(channelIndex);

                    bufferIndex = circBufWriteIndex;

                    for (sampleIndex = 0; sampleIndex < samplesPerBatch; sampleIndex++)
                    {
                        // The background thread is filling the buffers. This is where you empty them:
                        signalValue = headset.ReadBuffered(c);

                        // Store the signal value in an array for processing.
                        CircularBuffer[channelIndex, bufferIndex] = signalValue;
                        bufferIndex = (++bufferIndex) & (CIRCULAR_BUF_LEN - 1);
                    }
                }

                circBufWriteIndex = (circBufWriteIndex + samplesPerBatch) & (CIRCULAR_BUF_LEN - 1);

                // Continue computing min & max sample value, as well accumulated sum over 1.0 second interval
                for (channelIndex = 0; channelIndex < numberOfChannels; channelIndex++)
                {
                    bufferIndex = circBufReadIndex;

                    for (sampleIndex = 0; sampleIndex < samplesPerBatch; sampleIndex++)
                    {
                        signalValue = CircularBuffer[channelIndex, bufferIndex];
                        bufferIndex = (++bufferIndex) & (CIRCULAR_BUF_LEN - 1);

                        accumSum[channelIndex] = accumSum[channelIndex] + signalValue;

                        if (signalValue < minValue[channelIndex])
                        {
                            minValue[channelIndex] = signalValue;
                        }
                        else if (signalValue > maxValue[channelIndex])
                        {
                            maxValue[channelIndex] = signalValue;
                        }
                    }
                }

                /* ++++++++++++++++++++++++++++++++++++++ */

                // Pass the same sensor voltage values through a 100 Hz, low pass FIR filter
                // to generate a bandlimited noise level signal.
                for (channelIndex = 0; channelIndex < numberOfChannels; channelIndex++)
                {
                    bufferIndex = circBufReadIndex;
                    filterIndex = filterBufIndex;

                    for (sampleIndex = 0; sampleIndex < samplesPerBatch; sampleIndex++)
                    {
                        signalValue = CircularBuffer[channelIndex, bufferIndex];
                        bufferIndex = (++bufferIndex) & (CIRCULAR_BUF_LEN - 1);

                        delayedInputBuffer[channelIndex, filterState] = signalValue;
                        delayedInputBuffer[channelIndex, (filterState + ntaps)] = signalValue;

                        accum = 0.0;

                        for (int i = 0, j = filterState; i < ntaps; i++, j++)
                        {
                            accum += delayedInputBuffer[channelIndex, j] * FIR_LPF_100Hz_coeff[i];
                        }

                        FilterOutputBuffer[channelIndex, filterIndex] = accum;
                        filterIndex = (++filterIndex) & (CIRCULAR_BUF_LEN - 1);

                        accumFilteredSum[channelIndex] = accumFilteredSum[channelIndex] + accum;

                        if (--filterState < 0)
                        {
                            filterState += ntaps;
                        }
                    }
                }

                filterBufIndex = (filterBufIndex + samplesPerBatch) & (CIRCULAR_BUF_LEN - 1);
                circBufReadIndex = (circBufReadIndex + samplesPerBatch) & (CIRCULAR_BUF_LEN - 1);

                /* ++++++++++++++++++++++++++++++++++++++ */

                // Read one sensor impedance value for each channel once per batch.
                // (Impedance value doesn't change as rapidly as detected voltage level).
                for (sourceIndex = 0; sourceIndex < numberOfSources; sourceIndex++)
                {
                    IntPtr s = headset.GetSource(sourceIndex);

                    if (headset.IsReferentialEEG(s) && !headset.IsFactoryReference(s))
                    {
                        ImpedanceBuffer[sourceIndex] = headset.GetImpedanceEEG(s);
                    }
                    else
                    {
                        // The common-mode follower (CMF) sensor, at the factory reference position, is a special case:
                        ImpedanceBuffer[sourceIndex] = headset.GetImpedanceCMF();
                    }
                }

                // Process impedance statistics.
                for (sourceIndex = 0; sourceIndex < numberOfSources; sourceIndex++)
                {
                    sumImpedance[sourceIndex] = sumImpedance[sourceIndex] + ImpedanceBuffer[sourceIndex];

                    if (ImpedanceBuffer[sourceIndex] < minImpedance[sourceIndex])
                    {
                        minImpedance[sourceIndex] = ImpedanceBuffer[sourceIndex];
                    }
                    else if (ImpedanceBuffer[sourceIndex] > maxImpedance[sourceIndex])
                    {
                        maxImpedance[sourceIndex] = ImpedanceBuffer[sourceIndex];
                    }
                }

                /* ++++++++++++++++++++++++++++++++++++++ */

                // Check if one second has passed before print min, max, and avg sensor values over a second interval
                // Each batch consists of 15 samples taken over 50 msec
                if (batchCounter >= 20)
                {
                    batchSize = batchCounter * samplesPerBatch;

                    for (sourceIndex = 0; sourceIndex < numberOfSources; sourceIndex++)
                    {
                        averageValue = sumImpedance[sourceIndex] / (double)batchCounter;

                        statusTableValues[sourceIndex, 0] = averageValue;

                        sumImpedance[sourceIndex] = 0.0;
                        minImpedance[sourceIndex] = 0.0;
                        maxImpedance[sourceIndex] = 0.0;
                    }

                    for (channelIndex = 0; channelIndex < numberOfChannels; channelIndex++)
                    {
                        averageValue = accumSum[channelIndex] / (double)batchSize;

                        statusTableValues[channelIndex, 1] = averageValue;

                        statusTableValues[channelIndex, 3] = minValue[channelIndex];
                        statusTableValues[channelIndex, 4] = maxValue[channelIndex];

                        accumSum[channelIndex] = 0.0;

                        // Resetting min/max value to zero creates artificial floor or ceiling for next one
                        // second interval. Use last min/max samples from this batch as start point for next.
                        double temp = minValue[channelIndex];
                        minValue[channelIndex] = maxValue[channelIndex];
                        maxValue[channelIndex] = temp;

                        difference = 0.0;
                        variance = 0.0;

                        bufferIndex = startIntervalIndex;

                        averageValue = accumFilteredSum[channelIndex] / (double)batchSize;
                        accumFilteredSum[channelIndex] = 0.0;

                        for (int index = 0; index < batchSize; index++)
                        {
                            signalValue = FilterOutputBuffer[channelIndex, bufferIndex];
                            bufferIndex = (++bufferIndex) & (CIRCULAR_BUF_LEN - 1);

                            difference = signalValue - averageValue;
                            variance = variance + (difference * difference);
                        }

                        standardDeviation = Math.Sqrt((variance / (double)(batchSize - 1)));
                        statusTableValues[channelIndex, 2] = standardDeviation;
                    }

                    startIntervalIndex = (startIntervalIndex + batchSize) & (CIRCULAR_BUF_LEN - 1);

                    batchCounter = 0;

                    statusBuffer = PackStatusTableValues(numberOfSources, numStatusValues, statusTableValues);

                    bufferBlock.Post(statusBuffer);

                }  // if (batchCounter >= 20)

            }  // while (!cts.IsCancellationRequested)

        }  // MeasureConnectivity()

        /* ++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++ */
        /* ++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++ */

        public void RecordSensorData(BinaryWriter bw, double[,] statusTableValues, double stateMarker)
        {
            int numRows = statusTableValues.GetLength(0);
            int numCols = statusTableValues.GetLength(1);

            for (int j = 0; j < numCols; j++)
            {
                for (int i = 0; i < numRows; i++)
                {
                    bw.Write(statusTableValues[i, j]);
                }
            }

            for (int i = 0; i < numRows; i++)
            {
                bw.Write(stateMarker);
            }
        }

        /* ++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++ */
        /* ++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++ */

        public async Task<double[]> UpdataSensorStatusAsync(ISourceBlock<double[]> bufferBlock)
        {
            // Wait for MeasureConnectivity task to send measured sensor values over the
            // past 1 second interval back to the main (GUI) thread via the bufferBlock
            // construct found in the .NET Framework library.

            double[] statusBuffer = await bufferBlock.ReceiveAsync();

            return statusBuffer;
        }

        /* ++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++ */
        /* ++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++ */

        public double[] PackStatusTableValues(int numSources, int numStatusValues, double[,] valueTable)
        {
            int i = 0;

            double[] statusBuffer = new double[numSources * numStatusValues];

            for (int j = 0; j < numSources; j++)
            {
                for (int k = 0; k < numStatusValues; k++)
                {
                    statusBuffer[i++] = valueTable[j, k];
                }
            }
            return statusBuffer;
        }

        /* ++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++ */
        /* ++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++ */

        public double[,] UnpackStatusTableValues(int numSources, int numStatusValues, double[] statusBuffer)
        {
            int i = 0;

            double[,] valueTable = new double[numSources, numStatusValues];

            for (int j = 0; j < numSources; j++)
            {
                for (int k = 0; k < numStatusValues; k++)
                {
                    valueTable[j, k] = statusBuffer[i++];
                }
            }
            return valueTable;
        }

        /* ++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++ */
        /* ++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++ */

        public int[] UpdateStatusColorCode(double[,] statusTableValues, ref SensorEvents[] sensorEventCounters)
        {
            int channelIndex;
            int sourceIndex;

            int numberOfChannels = 7;
            int numberOfSources = 8;

            int[] statusColorCode = new int[8];

            int[,] statusSensorScores = new int[8, 3];

            double signalValue;

            // Determine color coded status for each sensor measurement.

            /* ****************************************************************
             * 
             * NOTE:  The sensor value ranges in the following code are
             *        specifically for the VR300 Wearable Sensing headset
             *        and they attempt to replicate the readings and color
             *        coded status reported by their DSI Streamer software
             *        tool during connectivity adjustment.
             *        
             * *************************************************************** */

            // Assign status score to average Baseline Voltage signal level.
            for (channelIndex = 0; channelIndex < numberOfChannels; channelIndex++)
            {
                signalValue = statusTableValues[channelIndex, 1];

                if ((signalValue < -9000.0) || (signalValue > 9000.0))
                {
                    statusSensorScores[channelIndex, 1] = 5;    // Red - requires twist thru hair and push down on sensor
                    sensorEventCounters[channelIndex].baselineLevelOutOfRange++;
                }
                else if ((signalValue < -5000.0) || (signalValue > 5000.0))
                {
                    statusSensorScores[channelIndex, 1] = 3;    // Yellow - push down on sensor, no twist thru hair
                    sensorEventCounters[channelIndex].baselineLevelAdjust++;
                }
                else  // value is between +/- 5000.0
                {
                    statusSensorScores[channelIndex, 1] = 0;    // Green - good
                    sensorEventCounters[channelIndex].baselineLevelGood++;
                }
            }

            // Assign status score to average Noise Level signal level.
            for (channelIndex = 0; channelIndex < numberOfChannels; channelIndex++)
            {
                signalValue = statusTableValues[channelIndex, 2];

                if (signalValue > 100.0)
                {
                    statusSensorScores[channelIndex, 2] = 5;    // Red
                    sensorEventCounters[channelIndex].noiseLevelOutOfRange++;
                }

                else if (signalValue > 50.0)
                {
                    statusSensorScores[channelIndex, 2] = 3;    // Yellow
                    sensorEventCounters[channelIndex].noiseLevelAdjust++;
                }
                else  // value is lower than 30.0 Vrms
                {
                    statusSensorScores[channelIndex, 2] = 0;    // Green
                    sensorEventCounters[channelIndex].noiseLevelGood++;
                }
            }

            // Assign status score to average Impedance value.
            for (sourceIndex = 0; sourceIndex < numberOfSources; sourceIndex++)
            {
                signalValue = statusTableValues[sourceIndex, 0];

                if (signalValue > 10.0)  // MegaOhms
                {
                    statusSensorScores[sourceIndex, 0] = 10;    // Bad - No sensor connection to scalp at all
                    sensorEventCounters[sourceIndex].impedanceBad++;
                }
                else if ((signalValue > 2.0) && (signalValue < 10.0))
                {
                    statusSensorScores[sourceIndex, 0] = 5;    // Red - requires twist thru hair and push down on sensor
                    sensorEventCounters[sourceIndex].impedancOutOfRange++;
                }
                else if ((signalValue > 1.0) && (signalValue < 2.0))
                {
                    statusSensorScores[sourceIndex, 0] = 3;    // Yellow - push down on sensor, no twist thru hair
                    sensorEventCounters[sourceIndex].impedanceAdjust++;
                }
                else if ((signalValue > 0.05) && (signalValue < 1.0))
                {
                    statusSensorScores[sourceIndex, 0] = 0;    // Green - good
                    sensorEventCounters[sourceIndex].impedanceGood++;
                }
                else  // value is lower than 0.05 MegaOhms
                {
                   Logger.Info(this,"Chan ="+ sourceIndex + ", Imped =" + signalValue);
                    statusSensorScores[sourceIndex, 0] = 10;    // Bad - Ear clip has fallen off
                    sensorEventCounters[sourceIndex].impedanceBad++;
                }
            }

            // Combine sesnor status scores to produce color code value for each sensor.
            for (sourceIndex = 0; sourceIndex < numberOfSources; sourceIndex++)
            {
                // statusColorCode[sourceIndex] = statusSensorScores[sourceIndex, 0] + statusSensorScores[sourceIndex, 1] + statusSensorScores[sourceIndex, 2];

                if ((statusSensorScores[sourceIndex, 0] >= 5) || (statusSensorScores[sourceIndex, 1] >= 5) || (statusSensorScores[sourceIndex, 2] >= 5))
                {
                    statusColorCode[sourceIndex] = 5;
                }
                else if ((statusSensorScores[sourceIndex, 0] == 3) || (statusSensorScores[sourceIndex, 1] == 3) || (statusSensorScores[sourceIndex, 2] == 3))
                {
                    statusColorCode[sourceIndex] = 3;
                }
                else
                {
                    statusColorCode[sourceIndex] = 0;
                }
            }

            return statusColorCode;
        }

        /* ++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++ */
        /* ++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++ */

        public void ResetSensorEventCounters(ref SensorEvents[] sensorEventCounters)
        {
            int numberOfSources = sensorEventCounters.Length;

            for (int sourceIndex = 0; sourceIndex < numberOfSources; sourceIndex++)
            {
                sensorEventCounters[sourceIndex].noiseLevelOutOfRange = 0;
                sensorEventCounters[sourceIndex].noiseLevelAdjust = 0;
                sensorEventCounters[sourceIndex].noiseLevelGood = 0;
                sensorEventCounters[sourceIndex].baselineLevelOutOfRange = 0;
                sensorEventCounters[sourceIndex].baselineLevelAdjust = 0;
                sensorEventCounters[sourceIndex].baselineLevelGood = 0;
                sensorEventCounters[sourceIndex].impedancOutOfRange = 0;
                sensorEventCounters[sourceIndex].impedanceAdjust = 0;
                sensorEventCounters[sourceIndex].impedanceGood = 0;
                sensorEventCounters[sourceIndex].impedanceBad = 0;
            }
        }

        /* ++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++ */
        /* ++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++ */

        public bool MakeSensorRecommendation(int threshold, SensorEvents[] sensorEventCounters, int[] recommendColorCode)
        {
            bool testPassed = true;         // Assume pass until find case where sensor needs adjustment

            int numberOfSources = sensorEventCounters.Length;

            for (int sourceIndex = 0; sourceIndex < numberOfSources; sourceIndex++)
            {
                if ((sensorEventCounters[sourceIndex].noiseLevelOutOfRange >= threshold) ||
                     (sensorEventCounters[sourceIndex].baselineLevelOutOfRange >= threshold) ||
                     (sensorEventCounters[sourceIndex].impedancOutOfRange >= threshold))
                {
                    recommendColorCode[sourceIndex] = 5;
                    testPassed = false;
                }

                else if (((sensorEventCounters[sourceIndex].noiseLevelOutOfRange + sensorEventCounters[sourceIndex].noiseLevelAdjust) >= threshold) ||
                     ((sensorEventCounters[sourceIndex].baselineLevelOutOfRange + sensorEventCounters[sourceIndex].baselineLevelAdjust) >= threshold) ||
                     ((sensorEventCounters[sourceIndex].impedancOutOfRange + sensorEventCounters[sourceIndex].impedanceAdjust) >= threshold))
                {
                    recommendColorCode[sourceIndex] = 3;
                    testPassed = false;
                }

                else
                {   // No adjustments needed
                    recommendColorCode[sourceIndex] = 0;
                }
            }
            return testPassed;
        }

        /* ++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++ */
        /* ++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++ */

        private void UpdateMeasurements(double[,] statusValues, int[] statusColor)
        {
            OnMeasurementsChange(null);
            List<SensorMeasurements> measurements = new List<SensorMeasurements>();
            measurements.Add(new SensorMeasurements() { Sensor = "Fz", Impedance = statusValues[0, 0], Level = statusValues[0, 1], Noise = statusValues[0, 2] });
            measurements.Add(new SensorMeasurements() { Sensor = "P3", Impedance = statusValues[1, 0], Level = statusValues[1, 1], Noise = statusValues[1, 2] });
            measurements.Add(new SensorMeasurements() { Sensor = "Pz", Impedance = statusValues[2, 0], Level = statusValues[2, 1], Noise = statusValues[2, 2] });
            measurements.Add(new SensorMeasurements() { Sensor = "P4", Impedance = statusValues[3, 0], Level = statusValues[3, 1], Noise = statusValues[3, 2] });
            measurements.Add(new SensorMeasurements() { Sensor = "PO7", Impedance = statusValues[4, 0], Level = statusValues[4, 1], Noise = statusValues[4, 2] });
            measurements.Add(new SensorMeasurements() { Sensor = "Oz", Impedance = statusValues[5, 0], Level = statusValues[5, 1], Noise = statusValues[5, 2] });
            measurements.Add(new SensorMeasurements() { Sensor = "PO8", Impedance = statusValues[6, 0], Level = statusValues[6, 1], Noise = statusValues[6, 2] });
            measurements.Add(new SensorMeasurements() { Sensor = "Ear", Impedance = statusValues[7, 0], Level = statusValues[7, 1], Noise = statusValues[7, 2] });
            // Render actual measured sensor values in the DataGrid of the display.
            OnMeasurementsChange(measurements);
            OnSensorStatusColorChange(statusColor);
        }

        public void UpdateHeadsetPort(string port)
        {
            WearableSensingVR300.defaultPort = port;
        }
    }
}
