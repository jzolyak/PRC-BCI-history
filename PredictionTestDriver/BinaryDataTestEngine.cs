using BCI_Headset;
using CoreServices.Common;
using CoreServices.Files;
using CoreServices.Filtering;
using CoreServices.FlashControl;
using CoreServices.Models;
using CoreServices.Recording;
using CoreServices.Stimulus;
using CoreServices.Presets;

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Threading.Tasks.Dataflow;
using System.Diagnostics;
using static CoreServices.SensorData.ReadBinaryFileData;

namespace CoreServices.SensorData
{
    public class BinaryDataTestEngine
    {

        /* ++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++ */

        private BinaryDataTestStates modeState;

        public enum BinaryDataTestStates
        {
            Open_Files, Core_Loop, Clean_Up, Exit_Mode
        }

        public string[] BinaryDataTestStateNames =
        {
            "Open_Files", "Core_Loop", "Clean_Up", "Exit_Mode"
        };

        public BinaryDataTestEngine()
        {
        }

        /* ++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++ */

        public async Task BinaryDataTestMode()
        {
            bool modeExit = false;

            int messageType = 0;
            //int interPacketDelay = 30;
            //int interPacketDelay = 2;
            int interPacketDelay = 15;

            //TransmitMethod transmitMethod = TransmitMethod.PushMethod;
            TransmitMethod transmitMethod = TransmitMethod.PullMethod;

            BinaryReader binaryReader = null;
            BinaryWriter binaryWriter = null;

            modeState = BinaryDataTestStates.Open_Files;

            Stopwatch stopwatch = new Stopwatch();


            /*   ***************************************
            **   ***  Instantiate container objects  ***   
            **   *************************************** 
            */

            // Temporarily use the constructor to assign default value for each preset until
            // we can fetch the real preset configuration values from a stored file.
            PredictionPresets predictionConfiguration = PredictionPresets.Instance;

            string currentDirectory = Directory.GetCurrentDirectory();
            string inputBinaryFilename = "TestInput5.bin";
            string inputFilePath = System.IO.Path.Combine(currentDirectory, inputBinaryFilename);

            string outputBinaryFilename = "TestOuput.bin";
            string outputfilePath = System.IO.Path.Combine(currentDirectory, outputBinaryFilename);


            /* ++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++ */
            /* ++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++ */

            stopwatch.Start();

            try
            {
                // Continue reading from binary input file and recording it back to output file until reach end of input file.
                while (!modeExit)
                {
                    string logString = "modeState = " + BinaryDataTestStateNames[(int)modeState] + "\n";
                    Logger.Info(this, logString);
                    // Console.WriteLine(logString);


                    switch (modeState)
                    {
                        /* ************* */
                        /*  Outer Loop   */
                        /* ************* */

                        case BinaryDataTestStates.Open_Files:
                            // Prediction mode needs to open 3 files:
                            //   1.  Weight Coefficient (aka "Calibration Data") file. The filename is stored and saved as part
                            //       of user configuration (meta) data. The file is opened here, its contents are read and stored
                            //       into a matrix, and then the file is immediately closed.
                            //   2.  Binary data file. Record raw sensor and marker information in binary format to file. Currently
                            //       I don't know where the file name originates from, so we will use dummy default name of
                            //       PredictionData.bin for now.
                            //   3.  Text file for logging Prediction related information. Currently I don't know where the file name
                            //       originates from, so we will use dummy default name of PredictionLog.txt for now.

                            try
                            {
                                binaryReader = new BinaryReader(new FileStream(inputFilePath, FileMode.Open));
                            }
                            catch (IOException e)
                            {
                                Logger.Info(this, "\n Cannot open input raw data binary file");
                                // logString = "\n Cannot open input raw data binary file";
                                // Console.WriteLine(logString);
                                return;
                            }

                            try
                            {
                                binaryWriter = new BinaryWriter(new FileStream(outputfilePath, FileMode.Create));
                            }
                            catch (IOException e)
                            {
                                Logger.Info(this, "\n Cannot open output raw data binary file.");
                                // logString = "\n Cannot open output raw data binary file";
                                // Console.WriteLine(logString);
                                return;
                            }

                            modeState = BinaryDataTestStates.Core_Loop;
                            break;


                        /* ********************************* */
                        /*  Real-time Processing Core Loop   */
                        /* ********************************* */

                        case BinaryDataTestStates.Core_Loop:

                            bool exitFlag = false;
                            bool readBinaryStatusReceived = false;


                            /* ********************************* */

                            CancellationTokenSource pipelineTokenSource = new CancellationTokenSource();
                            SemaphoreSlim _signal = new SemaphoreSlim(0, 1);

                            DataflowBlockOptions blockOptions = new DataflowBlockOptions();
                            blockOptions.CancellationToken = pipelineTokenSource.Token;

                            // Create instances of BufferBlocks to pass data between pipelined tasks.
                            var packetDataBufferBlock = new BufferBlock<double[]>(blockOptions);

                            var statusMsgBufferBlock = new BufferBlock<ReadBinaryStatusMessage>(blockOptions);

                            ReadBinaryStatusMessage readBinaryStatusMessage = new ReadBinaryStatusMessage();

                            // Create instances of classes that comprise the processing pipeline tasks.
                            ReadBinaryFileData readBinary = new ReadBinaryFileData();

                            RecordBinaryData recordBinary = new RecordBinaryData();

                            // Start separate tasks that fetch sensor samples from headset while flashing keys on display screen,
                            // filtering the data for each sensor channel, and recording the filtered sensor data.

                            Task t2 = Task.Run(() => readBinary.ReadFileDataAsync(pipelineTokenSource.Token, _signal, packetDataBufferBlock, statusMsgBufferBlock,
                                                        binaryReader, transmitMethod, interPacketDelay));

                            // Task t3 = Task.Run(() => recordBinary.RecordDataAsync(pipelineTokenSource.Token, _signal, packetDataBufferBlock, binaryWriter));
                            Task t3 = Task.Run(() => recordBinary.RecordDataAsync(pipelineTokenSource.Token, packetDataBufferBlock, binaryWriter));


                            /* ********************************* */

                            while (exitFlag == false)
                            {
                                // Next poll for any status messaged sent from the ReadBinary task...
                                readBinaryStatusReceived = statusMsgBufferBlock.TryReceive(out readBinaryStatusMessage);

                                if (readBinaryStatusReceived == true)
                                {
                                    readBinaryStatusReceived = false;

                                    messageType = readBinaryStatusMessage.content[0];

                                    if (messageType == REACHED_END_OF_FILE)
                                    {
                                        // Send message that you have successfully read all the data in the binary data file.
                                        Console.WriteLine("Successfully reached end of binary data file...");
                                        exitFlag = true;
                                    }
                                    else
                                    {
                                        // Send message that an error occurred reading binary data from files
                                        Console.WriteLine("Detected error reading binary data file...");
                                        exitFlag = true;        // Abandon Ship!!!
                                    }

                                }  // if (readBinaryStatusReceived == true)

                            }  // while (exitFlag == false)

                            /* +++++++++++++++++++++++++++++++++++++++ */

                            // Stop processing pipeline tasks by cancelling the shared token
                            pipelineTokenSource.Cancel();

                            await Task.Delay(100);

                            packetDataBufferBlock.Complete();

                            statusMsgBufferBlock.Complete();

                            stopwatch.Stop();

                            //int stopwatchElasped = stopwatch.Elapsed.Milliseconds;    // WARNING! This is a mistake!!!
                            //long stopwatchElasped = stopwatch.ElapsedMilliseconds;      // 
                            double stopwatchElasped = stopwatch.Elapsed.TotalMilliseconds;      // 


                            Console.WriteLine("Elapsed time = {0} \n", stopwatchElasped);

                            modeState = BinaryDataTestStates.Clean_Up;
                            break;

                        /* ********************************* */
                        /*  End Core Loop                    */
                        /* ********************************* */

                        case BinaryDataTestStates.Clean_Up:
                            // Close all open files and delete class objects
                            binaryWriter.Flush();
                            await Task.Delay(100);

                            binaryReader.Close();
                            binaryWriter.Close();

                            modeState = BinaryDataTestStates.Exit_Mode;
                            break;

                        case BinaryDataTestStates.Exit_Mode:
                            // Now ready to exit the test driver
                            modeExit = true;
                            break;

                    }  // switch (modeState)

                }  // while (!modeExit)

            }
            catch (Exception ex)
            {
                Logger.Error(this, "Exception in Binary Data Test Engine" + ex.ToString());
                // string logString = "\n Exception in Binary Data Test Engine\" + ex.ToString()";
                // Console.WriteLine(logString);
            }
        }

        /* ++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++ */
        /* ++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++ */

    }
}
