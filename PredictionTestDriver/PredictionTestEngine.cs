using BCI_Headset;
using CoreServices.Common;
using CoreServices.Files;
using CoreServices.Filtering;
using CoreServices.FlashControl;
using CoreServices.Models;
using CoreServices.Recording;
using CoreServices.Stimulus;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Threading.Tasks.Dataflow;
using System.Diagnostics;

using CoreServices.Headset;
using CoreServices.Presets;
using static System.Formats.Asn1.AsnWriter;
using static CoreServices.SensorData.ReadBinaryFileData;
using CoreServices.SensorData;
using static CoreServices.Prediction.PredictionControl;
using static CoreServices.Prediction.PredicitionReport;
using static CoreServices.Presets.DecisionLogicPresets;

namespace CoreServices.Prediction
{
    public class PredictionTestEngine
    {
        private PredictionTestStates modeState;

        public enum PredictionTestStates
        {
            Open_Files, Next_Data_File, Core_Loop, Clean_Up, Write_Report, Exit_Mode
        }

        public string[] PredictionTestStateNames =
        {
            "Open_Files", "Next_Data_File", "Core_Loop", "Clean_Up", "Write_Report", "Exit_Mode"
        };

        public class TestInputFileInfo
        {
            // The file name structure of the calibration .bin files is:
            //   "baseBinaryFilename" + "_" + "setNum" + "_" + "totalSetNum" + ".bin"
            // where the value for "setNum" is between the values for startTargetNum and endTartgetNum.
            // For example if baseBinaryFilename = "PatientIdNum", startTargetNum = 1, endTartgetNum = 5,
            // and totalSetNum = 5, then the offline Prediction test program will process 
            // PatientIdNum_1_5.bin thru PatientIdNum_5_5.bin as the test input data files.

            public int startTargetNum;
            public int endTartgetNum;
            public int totalSetNum;

            public string? baseBinaryFilename;    // without the .bin file extension

            // The weight coefficient file would be:  PatientIdNum.coef {including the .coef file extension}
            public string? coefficientFilename;
            public string? accuracyReportFilename;

            public TestInputFileInfo()
            {
                startTargetNum = 1;
                endTartgetNum = 5;
                totalSetNum = 5;
                baseBinaryFilename = null;
                coefficientFilename = null;
                accuracyReportFilename = "AccuracyReport.txt";
            }
        }

        public PredictionTestEngine()
        {
        }

        /* ++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++ */

        public int ParseTestInputFileInfo(string testInputFilename, ref TestInputFileInfo fileInfoValues)
        {
            //TestInputFileInfo fileInfoValues = new TestInputFileInfo();

            // For now just report that can't read from the file.
            // In future can range check values in the file.
            const int FILE_READ_ERROR = -1;

            int errorCode = 0;          // errorCode = 0 means parsing method succeeded
            int currentLine = 0;
            int maxLinesInFile = 0;

            string[] parameterLines = null;

            string currentDirectory = Directory.GetCurrentDirectory();
            string fileInfoPath = System.IO.Path.Combine(currentDirectory, testInputFilename);

            try
            {
                parameterLines = File.ReadAllLines(fileInfoPath);
                maxLinesInFile = parameterLines.Length;
                currentLine = 0;
            }
            catch (Exception ex)
            {
                return FILE_READ_ERROR;
            }

            while (currentLine < maxLinesInFile)
            {
                // Read file info parameters from list of strings in text file
                string[] fields = parameterLines[currentLine].Split(' ');

                // Parse command string contents into field values and decide what to do next
                if (fields[0] == "startTargetNum")
                {
                    fileInfoValues.startTargetNum = Int32.Parse(fields[1]);
                }
                else if (fields[0] == "endTartgetNum")
                {
                    fileInfoValues.endTartgetNum = Int32.Parse(fields[1]);
                }
                else if (fields[0] == "totalSetNum")
                {
                    fileInfoValues.totalSetNum = Int32.Parse(fields[1]);
                }
                else if (fields[0] == "baseBinaryFilename")
                {
                    fileInfoValues.baseBinaryFilename = fields[1];
                }
                else if (fields[0] == "coefficientFilename")
                {
                    fileInfoValues.coefficientFilename = fields[1];
                }
                else if (fields[0] == "accuracyReportFilename")
                {
                    fileInfoValues.accuracyReportFilename = fields[1];
                }

                currentLine++;
            }
            errorCode = 0;

            return errorCode;

        }  // ParseTestInputFileInfo()

        /* ++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++ */

        public int ParserOfflinePredictionParameters(string testInputFilename, ref PredictionPresets predictConfig)
        {

            // For now just report that can't read from the file.
            // In future can range check values in the file.
            const int FILE_READ_ERROR = -1;

            int errorCode = 0;
            int currentLine = 0;
            int maxLinesInFile = 0;

            string[] parameterLines = null;

            string currentDirectory = Directory.GetCurrentDirectory();
            string paramFilePath = System.IO.Path.Combine(currentDirectory, testInputFilename);

            try
            {
                parameterLines = File.ReadAllLines(paramFilePath);
                maxLinesInFile = parameterLines.Length;
                currentLine = 0;
            }
            catch (Exception ex)
            {
                return FILE_READ_ERROR;
            }

            while (currentLine < maxLinesInFile)
            {
                // Read file info parameters from list of strings in text file
                string[] fields = parameterLines[currentLine].Split(' ');

                // Parse command string contents into field values and decide what to do next
                if (fields[0] == "highestTopScores")
                {
                    predictConfig.PredictionCriteriaPresets.highestTopScores = Int32.Parse(fields[1]);
                }
                else if (fields[0] == "winCountForSelection")
                {
                    predictConfig.PredictionCriteriaPresets.winCountForSelection = Int32.Parse(fields[1]);
                }
                else if (fields[0] == "predictScoreThreshold")
                {
                    predictConfig.PredictionCriteriaPresets.predictScoreThreshold = Int32.Parse(fields[1]);
                }
                else if (fields[0] == "scoringMethod")
                {
                    predictConfig.DecisionLogicPresets.scoringMethod = (ScoringMethod)Int32.Parse(fields[1]);
                }
                else if (fields[0] == "stoppingMethod")
                {
                    predictConfig.DecisionLogicPresets.stoppingMethod = (StoppingMethod)Int32.Parse(fields[1]);
                }
                else if (fields[0] == "finalDecisionType")
                {
                    predictConfig.DecisionLogicPresets.finalDecisionType = (DecisionMethod)Int32.Parse(fields[1]);
                }
                else if (fields[0] == "maxNumFlashSequences")
                {
                    predictConfig.DecisionLogicPresets.maxNumFlashSequences = Int32.Parse(fields[1]);
                }
                else if (fields[0] == "scoringWindowLength")
                {
                    predictConfig.DecisionLogicPresets.scoringWindowLength = Int32.Parse(fields[1]);
                }
                else if (fields[0] == "AbstentionMessageEnabled")
                {
                    predictConfig.AbstentionPresets.AbstentionMessageEnabled = Int32.Parse(fields[1]);
                }
                else if (fields[0] == "AutoPauseAbstentionCount")
                {
                    predictConfig.AbstentionPresets.AutoPauseAbstentionCount = Int32.Parse(fields[1]);
                }

                currentLine++;
            }
            errorCode = 0;

            return errorCode;

        }  // ParserOfflinePredictionParameters()

        /* ++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++ */
        /* ++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++ */

        //public async Task PredictionTestMode()
        public async Task PredictionTestMode(TestInputFileInfo fileInfoValues, PredictionPresets predictionConfiguration)
        {
            /*
            // Prediction Status Message ID numbers
            const int END_OF_PREDICTION = 99;       // Report when reach max number of flash sequences or select Key
            const int MARK_STATE_CHANGE = 18;       // Report when stimulus changes state (for debug only)
            const int MARK_STIM_CHANGE = 20;        // Report when stimulus changes stim code (for debug only)
            const int PREDICTION_STATUS = 21;          // Detailed predict status at end of each flash sequence

            const int STIM_STATE_ERROR = 30;
            const int STIM_CODE_ERROR = 32;
            */

            bool modeExit = false;

            int readBinMessageType = 0;
            int predictMessageType = 0;
            int interPacketDelay = 2;

            // NOTE: During normal operation, sensor data is received from the headset and pushed down
            //       the processing pipeline to the Prediction task. In that scenario, the transmitMethod
            //       parameter should be set equal to PushMethod. In offline testing, waiting for 30 msec
            //       per packet would take forever to generate the Prediction Accuracy Report. Instead, 
            //       compile the code with the transmitMethod set equal to the PullMethod. As soon as the 
            //       sensor data packet is processed in the Prediction task, it signals to the ReadFileDataAsync() 
            //       task via a semaphore to fetch the sensor data from a file instead of the headset.

            //TransmitMethod transmitMethod = TransmitMethod.PushMethod;
            TransmitMethod transmitMethod = TransmitMethod.PullMethod;

            BinaryReader? inputReader = null;
            BinaryReader? coeffReader = null;
            StreamWriter? reportWriter = null;

            FileStream? binFileStream = null;

            int numTargets = 0;
            int maxNumFlashSeq = 0;

            int currentFileSetNum = 0;

            int messageLen;
            int indexValue;
            int stimCode;
            int phaseInSequence;
            int reportedAction;
            int elapsedTime;
            int sourceTimer;

            WeightCoefficients testWeightCoef = new WeightCoefficients();

            EndDecisionStrings decisionString = new EndDecisionStrings();
            PredictionAccuracyStrings accuracyString = new PredictionAccuracyStrings();

            // On first attempt I am using a fixed size static array set to maximum anticipated size for the
            // the number of flash sequences per target and number of targets in the calibration raw data.
            // Covert code to using linked lists in a future revision.
            ReportedResults[,] reportedResults = new ReportedResults[MAX_NUM_FLASH_SEQ, MAX_NUM_PREDICT_TARGETS];

            PredicitionReport predicitionReport = new PredicitionReport();

            List<DataSetInfo> dataSetInfo = new List<DataSetInfo>();

            DataSetInfo dataSetElement = new DataSetInfo();

            modeState = PredictionTestStates.Open_Files;


            /*   ****************************************
            **   ***  Offline Prediction Test Inputs  ***   
            **   **************************************** 
            */

            // Temporarily use the constructor to assign default value for each preset until
            // we can fetch the real preset configuration values from a stored file.
            // PredictionPresets predictionConfiguration = PredictionPresets.Instance;

            // NOTE: Calibration .bin file data is ALWAYS collected using a fixed number of flash sequences.
            //       Must override stopping method when using Calibration .bin files for Prediction.
            predictionConfiguration.DecisionLogicPresets.stoppingMethod = DecisionLogicPresets.StoppingMethod.FixedNumberOfSequences;

            // NOTE: Need to override Prediction preset value controlling the accumulated window size.
            //       Need to set window to max size so that accumulated scoring totals are based on entire
            //       flash sequences of data in order to get Prediction Accuracy measurements similar to BCI2000.
            predictionConfiguration.DecisionLogicPresets.scoringWindowLength = 15;

            // NOTE:  Before I started passing in the fileInfoValues parameter to the task
            //        I used the following hard coded values to match the test data files:
            //
            // int startFileSetNum = 1;
            // int endFileSetNum = 5;
            // int totalNumFileSets = 5;
            // string baseBinaryFilename = "H2NF5";
            // string coefficientFilename = "H2NF5.coef";
            // string reportFilename = "AccuracyReport.txt";

            int startFileSetNum = fileInfoValues.startTargetNum;
            int endFileSetNum   = fileInfoValues.endTartgetNum;
            int totalNumFileSets = fileInfoValues.totalSetNum;

            string baseBinaryFilename = fileInfoValues.baseBinaryFilename;
            string coefficientFilename = fileInfoValues.coefficientFilename;
            string reportFilename = fileInfoValues.accuracyReportFilename;

            string currentDirectory = Directory.GetCurrentDirectory();

            // string inputBinaryFilename = "TestInput5.bin";    // Raw data set files manually concatenated together into single file.
            // string baseBinaryFilename = "H2NF5";
            // string inputBinaryFilename = "H2NF5_1_5.bin";       // First raw data file in set of 5 files
            // string coefficientFilename = "H2NF5.coef";
            // string reportFilename = "AccuracyReport.txt";

            string startSetString = startFileSetNum.ToString();
            string totalSetString = totalNumFileSets.ToString();
            string inputBinaryFilename = baseBinaryFilename + "_" + startSetString + "_" + totalSetString + ".bin";

            string inputFilePath = System.IO.Path.Combine(currentDirectory, inputBinaryFilename);

            string coefficentfilePath = System.IO.Path.Combine(currentDirectory, coefficientFilename);

            string reportFilePath = System.IO.Path.Combine(currentDirectory, reportFilename);


            /* ++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++ */
            /* ++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++ */

            try
            {
                // Continue reading from binary input file and performing Prediction until reach end of input file.
                while (!modeExit)
                {
                    string logString = "modeState = " + PredictionTestStateNames[(int)modeState] + "\n";
                    Logger.Info(this, logString);

                    switch (modeState)
                    {
                        /* ************* */
                        /*  Outer Loop   */
                        /* ************* */


                        case PredictionTestStates.Open_Files:
                            // Prediction mode needs to open 3 files:
                            //   1.  Weight Coefficient (aka "Calibration Data") file. The filename is stored and saved as part
                            //       of user configuration (meta) data. The file is opened here, its contents are read and stored
                            //       into a matrix, and then the file is immediately closed.
                            //   2.  Binary data file. Record raw sensor and marker information in binary format to file. Currently
                            //       I don't know where the file name originates from, so we will use dummy default name of
                            //       PredictionData.bin for now.
                            //   3.  Text file for logging Prediction related information. Currently I don't know where the file name
                            //       originates from, so we will use dummy default name of PredictionLog.txt for now.

                            // Temporarily generate a set of weight coefficients that are easy to test for a pattern
                            // to ensure that they are received correctly by the Prediction task.
                            try
                            {
                                int index = 0;
                                coeffReader = new BinaryReader(new FileStream(coefficentfilePath, FileMode.Open));

                                while (coeffReader.BaseStream.Position != coeffReader.BaseStream.Length)
                                {
                                    testWeightCoef.values[index++] = coeffReader.ReadDouble();
                                }
                            }
                            catch (IOException e)
                            {
                                // Send error message to both logger file and console display.
                                Logger.Info(this, "\n Cannot open Weight Coefficient file");

                                // logString = "\n Cannot open Weight Coefficient file";
                                // Console.WriteLine(logString);
                                return;
                            }

                            // Construct first data set file name from driver input parameters.
                            currentFileSetNum = startFileSetNum;

                            // Keep track of which targets in the report are associated with each data set.
                            dataSetElement.currentSetNum = startFileSetNum;
                            dataSetElement.totalSetNum = totalNumFileSets;
                            dataSetElement.startTargetNum = 0;
                            dataSetElement.endTartgetNum = 0;       // Fill in later as process data

                            try
                            {
                                binFileStream = new FileStream(inputFilePath, FileMode.Open);
                                inputReader = new BinaryReader(binFileStream);
                            }
                            catch (IOException e)
                            {
                                // Send error message to both logger file and console display.
                                Logger.Info(this, "\n Cannot open input raw data binary file.");

                                // logString = "\n Cannot open input raw data binary file";
                                // Console.WriteLine(logString);
                                return;
                            }

                            try
                            {
                                // Append text to the existing file
                                // The 'true' parameter in the constructor enables append mode
                                reportWriter = new StreamWriter(reportFilePath, true);
                            }
                            catch (IOException e)
                            {
                                // Send error message to both logger file and console display.
                                Logger.Info(this, "\n Cannot open output report accuracy file.");

                                // logString = "\n Cannot open output report accuracy file";
                                // Console.WriteLine(logString);
                                return;
                            }

                            modeState = PredictionTestStates.Core_Loop;
                            break;

                        case PredictionTestStates.Next_Data_File:
                            // Close current processed binary data file.
                            binFileStream.Flush();
                            binFileStream.Close();
                            inputReader.Close();

                            // Update data set number to work on next.
                            currentFileSetNum++;
                            string currentSetString = currentFileSetNum.ToString();

                            // Determine next data set file to open and construct file name string.
                            if (currentFileSetNum <= endFileSetNum)
                            {
                                // Keep track of which targets in the report are associated with each data set.
                                dataSetElement.currentSetNum = currentFileSetNum;
                                dataSetElement.totalSetNum = totalNumFileSets;
                                dataSetElement.startTargetNum = numTargets;
                                dataSetElement.endTartgetNum = 0;       // Fill in later as process data

                                inputBinaryFilename = baseBinaryFilename + "_" + currentSetString + "_" + totalSetString + ".bin";

                                try
                                {
                                    // Open next data set file for processing
                                    inputFilePath = System.IO.Path.Combine(currentDirectory, inputBinaryFilename);

                                    //inputReader = new BinaryReader(new FileStream(inputFilePath, FileMode.Open));

                                    binFileStream = new FileStream(inputFilePath, FileMode.Open);
                                    inputReader = new BinaryReader(binFileStream);
                                }
                                catch (IOException e)
                                {
                                    // Send error message to both logger file and console display.
                                    Logger.Info(this, "\n Cannot open input raw data binary file.");

                                    // logString = "\n Cannot open input raw data binary file";
                                    // Console.WriteLine(logString);
                                    return;
                                }

                                modeState = PredictionTestStates.Core_Loop;
                            }
                            else
                            {
                                // Reached last data set, time to clean up, write report, and exit.
                                modeState = PredictionTestStates.Clean_Up;
                            }
                            break;


                        /* ********************************* */
                        /*  Real-time Processing Core Loop   */
                        /* ********************************* */

                        case PredictionTestStates.Core_Loop:

                            bool exitFlag = false;
                            bool readBinaryStatusReceived = false;
                            bool predictStatusReceived = false;

                            int fseq;
                            int code1;
                            int code2;
                            int code3;
                            int code4;
                            int code5;

                            int iPoint1;
                            int iPoint2;

                            int score1;
                            int score2;
                            int score3;
                            int score4;
                            int score5;
                            int predKey;
                            int winCnt;
                            int selKey;
                            int attKey;
                            int decision;
                            int accuracy;

                            /* ********************************* */
                            /* ********************************* */

                            CancellationTokenSource pipelineTokenSource = new CancellationTokenSource();
                            SemaphoreSlim _signal = new SemaphoreSlim(0, 1);

                            DataflowBlockOptions blockOptions = new DataflowBlockOptions();
                            blockOptions.CancellationToken = pipelineTokenSource.Token;

                            // Create instances of BufferBlocks to pass data between pipelined tasks.
                            var packetDataBufferBlock = new BufferBlock<double[]>(blockOptions);
                            var weightCoefBufferBlock = new BufferBlock<WeightCoefficients>(blockOptions);

                            // Create instances of BufferBlocks to pass message between tasks and test engine
                            var commandMsgBufferBlock = new BufferBlock<int[]>(blockOptions);
                            var predictStatusMsgBufBlock = new BufferBlock<PredictStatusMessage>(blockOptions);
                            var readBinStatusMsgBufBlock = new BufferBlock<ReadBinaryStatusMessage>(blockOptions);

                            // Create instance of storage buffer to receive message read from buffer block
                            ReadBinaryStatusMessage readBinaryStatusMessage = new ReadBinaryStatusMessage();
                            PredictStatusMessage predictStatusMessage = new PredictStatusMessage();

                            // Create instances of classes that comprise the processing pipeline tasks
                            ReadBinaryFileData readBinary = new ReadBinaryFileData();
                            PredictionControl predictionControl = new PredictionControl();

                            // Start separate tasks that ...
                            Task t2 = Task.Run(() => readBinary.ReadFileDataAsync(pipelineTokenSource.Token, _signal, packetDataBufferBlock, readBinStatusMsgBufBlock,
                                                        inputReader, transmitMethod, interPacketDelay));

                            Task t4 = Task.Run(() => predictionControl.PredictionControlAsync(pipelineTokenSource.Token, predictionConfiguration,
                                                        weightCoefBufferBlock, packetDataBufferBlock, commandMsgBufferBlock, predictStatusMsgBufBlock,
                                                        _signal, transmitMethod));


                            /* ********************************* */

                            while (exitFlag == false)
                            {

                                // Next poll for any status messaged sent from the ReadBinary task...
                                readBinaryStatusReceived = readBinStatusMsgBufBlock.TryReceive(out readBinaryStatusMessage);

                                if (readBinaryStatusReceived == true)
                                {
                                    readBinaryStatusReceived = false;

                                    readBinMessageType = readBinaryStatusMessage.content[0];

                                    if (readBinMessageType == REACHED_END_OF_FILE)
                                    {
                                        // Send message that you have successfully read all the data in the binary data file.
                                        Console.WriteLine("Successfully reached end of binary data file " + inputBinaryFilename + "\n");
                                        Logger.Info(this, "Successfully reached end of binary data file " + inputBinaryFilename + "\n");

                                        exitFlag = true;
                                    }
                                    else
                                    {
                                        // Send message that an error occurred reading binary data from files
                                        // Console.WriteLine("Detected error reading binary data file " + inputBinaryFilename + "\n");
                                        Logger.Info(this, "Detected error reading binary data file..." + inputBinaryFilename + "\n");

                                        exitFlag = true;        // Abandon Ship!!!
                                    }

                                }  // if (readBinaryStatusReceived == true)

                                /* ********************************* */

                                // Next poll for any status or debug messaged sent from the Prediction task...
                                predictStatusReceived = predictStatusMsgBufBlock.TryReceive(out predictStatusMessage);

                                if (predictStatusReceived == true)
                                {
                                    predictStatusReceived = false;

                                    predictMessageType = predictStatusMessage.content[0];

                                    if (predictMessageType == (int)PredictControlMsgID.MARK_STATE_CHANGE)
                                    {
                                        messageLen = predictStatusMessage.content[1];
                                        phaseInSequence = predictStatusMessage.content[2];
                                        stimCode = predictStatusMessage.content[3];
                                        reportedAction = predictStatusMessage.content[4];

                                        //Debug.WriteLine("{0}  MARK_STATE,  seq num = {1},  phase = {2},   time = {3} \n", flashTime, stimCode, phaseInSequence, sourceTimer);
                                        //Debug.WriteLine("{0}  MARK_STATE,  phase = {1}, seq num = {2}, action = {3} \n", flashTime, phaseInSequence, stimCode, reportedAction);
                                        //Logger.Info("STATE MARKER,  phaseInSeq = " + phaseInSequence + "  stim code1 = " + stimCode + "   action = " + reportedAction);

                                        if (phaseInSequence == 1)
                                        {
                                            weightCoefBufferBlock.Post(testWeightCoef);
                                        }
                                    }

                                    else if (predictMessageType == (int)PredictControlMsgID.MARK_STIM_CHANGE)
                                    {
                                        messageLen = predictStatusMessage.content[1];
                                        phaseInSequence = predictStatusMessage.content[2];
                                        stimCode = predictStatusMessage.content[3];
                                        reportedAction = predictStatusMessage.content[4];

                                        //Logger.Info("STIM  MARKER,  phaseInSeq = " + phaseInSequence + "  stim code1 = " + stimCode + "   action = " + reportedAction);
                                    }

                                    else if (predictMessageType == (int)PredictControlMsgID.PREDICTION_STATUS)
                                    {
                                        messageLen = predictStatusMessage.content[1];
                                        fseq  = predictStatusMessage.content[2];
                                        code1 = predictStatusMessage.content[3];
                                        code2 = predictStatusMessage.content[4];
                                        code3 = predictStatusMessage.content[5];
                                        code4 = predictStatusMessage.content[6];
                                        code5 = predictStatusMessage.content[7];

                                        iPoint1 = predictStatusMessage.content[8];
                                        iPoint2 = predictStatusMessage.content[9];

                                        score1 = predictStatusMessage.content[10];
                                        score2 = predictStatusMessage.content[11];
                                        score3 = predictStatusMessage.content[12];
                                        score4 = predictStatusMessage.content[13];
                                        score5 = predictStatusMessage.content[14];

                                        predKey = predictStatusMessage.content[15];
                                        winCnt = predictStatusMessage.content[16];
                                        selKey = predictStatusMessage.content[17];
                                        attKey = predictStatusMessage.content[18];
                                        decision = predictStatusMessage.content[19];
                                        accuracy = predictStatusMessage.content[20];

                                        // Keep track of the maximum number of flash sequences per target
                                        if (fseq > maxNumFlashSeq)
                                        {
                                            maxNumFlashSeq = fseq;
                                        }

                                        // Save a copy of the prediction results after each flash sequence to
                                        // generate a prediction accuracy report after prediction mode is finished.
                                        reportedResults[fseq, numTargets].predictedKeyID = predKey;
                                        reportedResults[fseq, numTargets].selectedKeyID = selKey;
                                        reportedResults[fseq, numTargets].attendedKeyID = attKey;
                                        reportedResults[fseq, numTargets].decision = decision;
                                        reportedResults[fseq, numTargets].accuracy = accuracy;

                                        // Store copy of prediction status for each flash sequence.
                                        string subString1 = "Decision = " + decisionString.GetString(decision);
                                        string subString2 = "  Predict keyID = " + predKey + "  Select keyID = " + selKey + "  Attend keyID = " + attKey;
                                        string subString3 = "  Accuracy = " + accuracyString.GetString(accuracy);

                                        if (decision == (int)EndDecision.STATUS_UPDATE_MSG)
                                        {
                                            Logger.Info("STATUS,  fseq = " + fseq + "  code1 = " + code1 + "   code2 = " + code2 + "      code3 = " + code3 + "       code4 = " + code4 + "       code5 = " + code5 + "      iPoint1 = " + iPoint1 + "  iPoint2 = " + iPoint2);
                                            Logger.Info("                score1 = " + score1 + "  score2 = " + score2 + "   score3 = " + score3 + "   score4 = " + score4 + "  score5 = " + score5 + "   predKey = " + predKey + "  winCnt = " + winCnt + "  selKey = " + selKey + "\n");
                                            Logger.Info(subString1 + subString2 + subString3 + "\n");
                                        }
                                        else
                                        {
                                            // End of prediction
                                            exitFlag = true;

                                            // Increment the number of target prediction made.
                                            numTargets++;

                                            Logger.Info("SELECT,  fseq = " + fseq + "  code1 = " + code1 + "   code2 = " + code2 + "      code3 = " + code3 + "       code4 = " + code4 + "       code5 = " + code5 + "      iPoint1 = " + iPoint1 + "  iPoint2 = " + iPoint2);
                                            Logger.Info("                score1 = " + score1 + "  score2 = " + score2 + "   score3 = " + score3 + "   score4 = " + score4 + "  score5 = " + score5 + "   predKey = " + predKey + "  winCnt = " + winCnt + "  selKey = " + selKey + "\n");
                                            Logger.Info(subString1 + subString2 + subString3 + "\n");
                                            Logger.Info("\n\n\n ************************ \n\n");

                                            // Console.WriteLine("selected keyID = {0} \n", selKey);
                                            Logger.Info(this, "selected keyID = " + selKey  + "\n");
                                        }
                                    }

                                    else if (predictMessageType == (int)PredictControlMsgID.STIM_STATE_ERROR)
                                    {
                                        messageLen = predictStatusMessage.content[1];
                                        phaseInSequence = predictStatusMessage.content[2];
                                        stimCode = predictStatusMessage.content[3];
                                        reportedAction = predictStatusMessage.content[4];

                                        Logger.Info("STIM STATE ERROR,  phaseInSeq = " + phaseInSequence + "  stim code1 = " + stimCode + "   action = " + reportedAction);
                                    }

                                    else if (predictMessageType == (int)PredictControlMsgID.STIM_CODE_ERROR)
                                    {
                                        messageLen = predictStatusMessage.content[1];
                                        phaseInSequence = predictStatusMessage.content[2];
                                        stimCode = predictStatusMessage.content[3];
                                        reportedAction = predictStatusMessage.content[4];

                                        Logger.Info("STIM CODE ERROR,  phaseInSeq = " + phaseInSequence + "  stim code1 = " + stimCode + "   action = " + reportedAction);
                                    }

                                }  // if (predictStatusReceived == true)
                                else
                                {
                                    // Clear out previous stale message header
                                    predictMessageType = 0;
                                }

                            }  // while (exitFlag == false)

                            /* +++++++++++++++++++++++++++++++++++++++ */

                            // Stop processing pipeline tasks by cancelling the shared token
                            pipelineTokenSource.Cancel();

                            await Task.Delay(1000);

                            packetDataBufferBlock.Complete();
                            weightCoefBufferBlock.Complete();

                            commandMsgBufferBlock.Complete();
                            readBinStatusMsgBufBlock.Complete();
                            predictStatusMsgBufBlock.Complete();

                            // Determine which path to take next: process next target in same file, 
                            // continue processing using data from next data set file, or done with data.
                            if (predictMessageType == (int)PredictControlMsgID.PREDICTION_STATUS)
                            {
                                // Reached the end of data for one target and made a Prediction.
                                // Release all the building blocks and rebuild the pipeline over again for next Prediction.
                                modeState = PredictionTestStates.Core_Loop;
                            }
                            else if (readBinMessageType == REACHED_END_OF_FILE)
                            {
                                // Save the current set and associated targets to list for writing report.
                                dataSetInfo.Add(dataSetElement);

                                // Reached end of binary data file. Move onto next data set file.
                                modeState = PredictionTestStates.Next_Data_File;
                            }
                            else
                            {
                                // Encountered a read error. Exit test engine.
                                modeState = PredictionTestStates.Clean_Up;
                            }
                            break;

                        /* ********************************* */
                        /*  End Core Loop                    */
                        /* ********************************* */


                        case PredictionTestStates.Clean_Up:
                            // Close all open files and delete class objects
                            inputReader.Close();
                            coeffReader.Close();

                            modeState = PredictionTestStates.Write_Report;
                            break;

                        case PredictionTestStates.Write_Report:
                            // Open text file handler to write the report to external file.
                            // Currently the report information is just written to the Logger.Info file for now.

                            // Take all the collected prediction results and generate accuracy report.
                            // predicitionReport.GenerateFlashSeqAccuracyReport(reportedResults, maxNumFlashSeq+1, numTargets);
                            predicitionReport.GenerateFlashSeqAccuracyReport(reportedResults, maxNumFlashSeq+1, numTargets, reportWriter);

                            reportWriter.Close();
                            modeState = PredictionTestStates.Exit_Mode;

                            break;

                        case PredictionTestStates.Exit_Mode:
                            // Send message that processing is finished
                            // Console.WriteLine("Processing finished. Press ENTER key to close window ... \n");

                            // Now ready to return to the BCI app Main Menu
                            modeExit = true;
                            break;

                    }  // switch (modeState)

                }  // while (!modeExit)
            }

            catch (Exception ex)
            {
                Logger.Error(this, "Exception in state engine" + ex.ToString());
                // string logString = "\n Exception in Binary Data Test Engine\" + ex.ToString()";
                // Console.WriteLine(logString);
            }
        }

        /* ++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++ */

    }
}
