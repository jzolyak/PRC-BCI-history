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

namespace CoreServices.Prediction
{
    public class PredictionStateEngine
    {
        private RemoteDisplay remoteDisplay = new RemoteDisplay();
        private FlashStimulus flashStimulus = new FlashStimulus();
        private PredictionStates modeState;
        private UIInstruction uiInstruction;
        private Action<UIInstruction> notifyUIAction;
        private Action<ScreenOverlay> notifyScreenOverLayChange;
        private bool _forceExit;

        private PredictionFileMetadata predictionFileMetadata;    // Still need ???

        private Action exitAction;
        private Action completeAction;
        private Action pauseAction;

        public enum PredictionStates
        {
            Show_Instructions, Open_Files, Remote_Display, Get_Remote_Elements,
            Local_Display, Get_Local_Elements, Assign_Key_Mapping, Create_Grouping, Create_Sequence,
            Core_Loop, Click_Key, Programmed_Delay, Monitor_Prediction_Interfaces,
            Clean_Up, Exit_Mode, Notify_UI, Wait_On_UI, Process_UI_Notification, Read_Command
        }

        public string[] PredictionStateNames =
        {
            "Show_Instructions", "Open_Files", "Remote_Display", "Get_Remote_Elements",
            "Local_Display", "Get_Local_Elements", "Assign_Key_Mapping", "Create_Grouping", "Create_Sequence",
            "Core_Loop", "Click_Key", "Programmed_Delay", "Monitor_Prediction_Interfaces",
            "Clean_Up", "Exit_Mode", "Notify_UI", "Wait_On_UI", "Process_UI_Notification", "Read_Command"
        };

        public PredictionStateEngine(Action<UIInstruction> notifyUIAction,Action<ScreenOverlay> notifyScreenOverLayChange, Action exitAction,Action completeAction,Action pauseAction)
        {
            this.notifyUIAction = notifyUIAction;
            this.notifyScreenOverLayChange = notifyScreenOverLayChange;
            this.exitAction = exitAction;
            this.completeAction = completeAction;
            this.pauseAction = pauseAction;

        }

        public async Task PredictionMode()
        {

            GroupingParam inputParam;
            inputParam.method = Method.NewMethod;
            inputParam.keyOrder = KeyOrder.Switchback;
            inputParam.boardDivision = BoardDivision.Checkerboard;
            inputParam.arrangement = Arrangement.Magic;
            inputParam.indexMapping = IndexMapping.GridSize;

            // PRC_mode eventually needs to be a controllable preset value to support different key grids.
            bool PRC_mode = true;

            //bool calibrationCompleted = false;

            //int startSetNum = 1;
            //int totalSetNum = 0;
            //int currentSetNum = 0;
            //int targetsReadFromSet = 0;

            int numGetElementRetries = 0;
            //int repeatCalibrationLine = 0;      // Still need ???
            //int currentLine = 0;
            //int maxLinesInFile = 0;
            int targetkeyIDvalue = 0;
            int selectedKeyIDvalue = 0;
            int matrixSize1 = 0;
            int matrixSize2 = 0;
            //bool showCountDownTimer = true;

            int elementIndex = 0;

            int UI_delay_msec = 200;
            int programmable_delay_msec = 1000;

            //string targetKeyName;
            //string[] commandLines = null;

            bool modeExit = false;
            //bool foundMatch = false;

            const int MAX_FLASH_MSG_LENGTH = 8;
            const int MAX_PREDICT_MSG_LENGTH = 20;

            const int FLASH_ON = 1;
            const int FLASH_OFF = 2;
            const int END_SEQUENCE = 3;

            const int ENABLE_PIPELINE_SENSOR_DATA = 1;
            const int DISABLE_PIPELINE_SENSOR_DATA = 2;

            const int samplesPerBatch = 9;              // 9 samples = 30 msec @ Fs=300 Hz
            const double bufferAheadSec = 0.100;        // 100 msec

            int numTargets = 1;

            int messageType;
            int messageLen;
            int indexValue;
            int stimCode;
            int phaseInSequence;
            int reportedAction;
            int elapsedTime;
            int sourceTimer;

            WeightCoefficients testWeightCoef = new WeightCoefficients();

            
            modeState = PredictionStates.Open_Files;


            UserSettingsService userSettingsService = new UserSettingsService();
            UI_delay_msec = Convert.ToInt32(userSettingsService.GetSettings("UI_delay_msec"));

            /*   ***************************************
            **   ***  Instantiate container objects  ***   
            **   *************************************** 
            */
            List<ElementProperties> elementList = new List<ElementProperties>();

            int[,] keyIDMap = null;
            int[,,] groupingMatrix = null;

            GridLayout gridData = new GridLayout();

            var stimulusOverlay = new ScreenOverlay();

            List<KeyMapping> flashableKeys = new List<KeyMapping>();

            List<FlashTableEntry> flashSequenceTable = new List<FlashTableEntry>();

            // Temporarily use the constructor to assign default value for each preset until
            // we can fetch the real preset configuration values from a stored file.

            //PredictionPresets predictionConfiguration = new Presets.PredictionPresets();
            var predictionPresets = PredictionPresets.Instance;  

            PredictionPresets predictionConfiguration = predictionPresets;
            //string currentDirectory = Directory.GetCurrentDirectory();
            //string coefficientFilename = "WeightCoefficients.coef";

            string currentDirectory = predictionFileMetadata.FilePath;
            string coefficientFilename =predictionFileMetadata.Name+"_current.COEF";
            string filePath = System.IO.Path.Combine(currentDirectory, coefficientFilename);

            // string currentDirectory = Directory.GetCurrentDirectory();
            //string scriptFilename = "CalibrationCommands.txt";
            //string filePath = System.IO.Path.Combine(currentDirectory, scriptFilename);


            /*   *****************************
            **   ***   Configure Headset   ***   
            **   *****************************
            */

            WearableSensingVR300 headset = new WearableSensingVR300();

            Logger.Info(this,"\nEntered program... \n");

            headset.CreateNew();
            headset.SetMessageVerbosity(2);

            string port = headset.GetDefaultHeadsetComPort();

            bool done = false;
            bool passedTest = false;
            int failedCOunt = 0;

            while (!done)
            {
                if(headset.ConnectPort(port))
                {
                    Logger.Info(this,"Headset connected using port"+port);
                }
                await Task.Delay(100);
                Logger.Info(this,"After the delay");
                passedTest = headset.IsConnected();

                if (passedTest == false)
                {
                    // Increment the number of failed connection attempts
                    if (++failedCOunt > 3)
                    {
                        // Just give up and tell user of problem for now.
                        done = true;
                    }
                    else
                    {
                        // Retry making the connection
                        headset.Disconnect();
                        await Task.Delay(100);
                    }      
                }
                else
                {   // Connected properly
                    done = true;
                }
            }

            if (passedTest == true)
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

            // Start the headset driver at the operating system level
            headset.StartBackgroundAcquisition();


            /* ++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++ */

            // 
            // The following code changes the design so that data acquisition starts at the
            // beginning of the mode and stops at the end of the mode. The data must be constantly
            // read from the headset, otherwise the buffers in the bluetooth driver will overflow.
            // A new headset task has been created and started outside the core loop. The data
            // is constantly read from the headset as soon as it arrives. The state engine
            // controls the data flow to the processing pipeline by sending a command to the
            // headset task to enable its output to the Buffer Block at the head of the pipeline.
            //

            // Create the TPL Dataflow pipeline pieces and start the new headset control task.
            HeadsetControl headsetControl = new HeadsetControl();

            CancellationTokenSource headsetTokenSource = new CancellationTokenSource();

            var headsetDataBufferBlock = new BufferBlock<double[]>();
            var headsetMsgBufferBlock = new BufferBlock<HeadsetControlMessage>();

            HeadsetControlMessage headsetControlMessage = new HeadsetControlMessage();


            Task t0 = Task.Run(() => headsetControl.ReadHeadsetDataAsync(headset, headsetTokenSource.Token, headsetDataBufferBlock, headsetMsgBufferBlock));


            /* ++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++ */

            try
            {
                // Don't need binaryWriter for Prediction to work. JAZ 4-12-24
                //BinaryWriter? binaryWriter = null;

                /*
                commandLines = File.ReadAllLines(filePath);
                maxLinesInFile = commandLines.Length;
                currentLine = 0;
                repeatCalibrationLine = 0;
                if(calibrationFileMetadata != null)
                {
                    startSetNum = calibrationFileMetadata.CurrentSet;
                }
                */

                // Continue performing Prediction until user selects button to return to BCI Menu
                while (!modeExit)
                {
                    string logString = "modeState = " + PredictionStateNames[(int)modeState] + "\n";
                    Logger.Info(this, logString);

                    switch (modeState)
                    {
                        /* ************* */
                        /*  Outer Loop   */
                        /* ************* */

                        case PredictionStates.Wait_On_UI:
                            do
                            {
                                await Task.Delay(UI_delay_msec);

                            } while (PredictionStates.Wait_On_UI == modeState);
                            break;

                        case PredictionStates.Process_UI_Notification:

                            if (uiInstruction == null) { modeState = PredictionStates.Clean_Up; break; }

                            /*
                             *  NOTE TO SEKHAR:
                             *  
                             *  Prediction mode for MVP demo only needs 2 things for the user interface:
                             *  
                             *  1.  Create an Instruction window similar to the one for Calibration. This window needs to 
                             *      have a Start and Cancel button just like in Calibration.
                             *      
                             *  2.  Need to simulate a "BCI Menu" button that allows the user to exit Predicition mode
                             *      and return to the BCI Main Menu after closing files and cleaning. 
                             *      

                            if (uiInstruction.Type == UIType.PredictionStopQuery)
                            {
                                if (uiInstruction.Result == true)
                                {
                                    //    // User selected Keep button to save raw data collected in the Calibration set.
                                    //    // Advance startSetNum to indicate which is the next set in the script to run.
                                    startSetNum++;
                                    if (binaryWriter != null)
                                    {
                                        FileHandlerService.CloseBinaryWriter(binaryWriter);
                                    }
                                    // Close raw data file for this calibration set

                                    if (startSetNum > totalSetNum)
                                    {
                                        // Reached last set in the script file. Inform user calibration is finished.
                                        //Handover the calib files to generate coef and move the bin files to completed folder
                                        modeState = CalibrationStates.Wait_On_UI;
                                        uiInstruction.Type = UIType.CalibrationComplete;
                                        calibrationCompleted = true;
                                        calibrationFileMetadata.CurrentSet = calibrationFileMetadata.NumofSets;
                                        calibrationFileMetadata.IsCurrent = false;
                                        uiInstruction.Result = false;
                                        NotifyUI(uiInstruction);
                                    }
                                    else
                                    {
                                        // Ask user if they want to continue with next calibration set
                                        modeState = PredictionStates.Ready_To_Begin;
                                    }
                                }
                                else
                                {
                                    Logger.Info(this, "deleting the current calibration set file:" + calibrationFileMetadata.GetCurrentBinFileName());
                                    FileHandlerService.DeleteBinaryFile(calibrationFileMetadata.GetCurrentBinFileName(), "CalibrationFiles");
                                    modeState = PredictionStates.Wait_On_UI;
                                    uiInstruction.Type = UIType.CalibrationRepeatQuery;
                                    uiInstruction.Result = false;
                                    NotifyUI(uiInstruction);
                                }
                            }
                            else if (uiInstruction.Type == UIType.CalibrationRepeatQuery)
                            {
                                if (uiInstruction.Result == true)
                                {
                                    // Need to create and open new file for next new calibration data set

                                    // Return to where we started current set in order to repeat it
                                    currentLine = repeatCalibrationLine;
                                    modeState = PredictionStates.Read_Command;
                                }
                                else
                                {
                                    // Clean up and return to Prediction submenu (not main BCI menu)
                                    calibrationFileMetadata.CurrentSet = calibrationFileMetadata.CurrentSet - 1;
                                    if (pauseAction != null) pauseAction();
                                    modeState = PredictionStates.Clean_Up;
                                }
                            }
                            else if (uiInstruction.Type == UIType.CalibrationBeginQuery)
                            {
                                if (uiInstruction.Result == true)
                                {
                                    // User selected Start button to continue with raw data collection in next calibration set.

                                    // Need to create and open new file for next new calibration data set

                                    // Proceed with reading commands from the script file for next calibration set
                                    showCountDownTimer = true;
                                    modeState = PredictionStates.Read_Command;
                                }
                                else
                                {   // User selected Cancel button to halt further raw data collection and will resume later.
                                    // Clean up and return to Calibration submenu (not main BCI menu)
                                    if (pauseAction != null) pauseAction();
                                    modeState = PredictionStates.Clean_Up;
                                }
                            }
                            else if (uiInstruction.Type == UIType.CountDownWindow)
                            {
                                if (uiInstruction.Result == true)
                                {
                                    showCountDownTimer = false;
                                    modeState = PredictionStates.Highlight_Target;
                                }
                                else
                                {
                                    // Clean up and return to Calibration submenu (not main BCI menu)
                                    if (calibrationFileMetadata!=null && calibrationFileMetadata.CurrentSet > 1)
                                    {
                                        calibrationFileMetadata.CurrentSet = calibrationFileMetadata.CurrentSet - 1;
                                    }
                                    if (pauseAction != null) pauseAction();
                                    modeState = PredictionStates.Clean_Up;
                                }
                            }
                            else if(uiInstruction.Type == UIType.CalibrationComplete)
                            {
                                modeState = PredictionStates.Clean_Up;
                            }
                            */
                            modeState = PredictionStates.Show_Instructions;
                            break;

                        case PredictionStates.Show_Instructions:
                            // Rather than immediately starting flashing for Prediction, show instruction popup window.
                            // Window will simply have Start and Cancel buttons to either start flashing for Prediction
                            // or return to BCI Main Menu if Cancel is selected.
                            // This window will eventually contain instructions on how to perform Pause, Resume, and Exit
                            // inside Prediction mode.

                            modeState = PredictionStates.Open_Files;
                            break;

                        case PredictionStates.Open_Files:
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
                                BinaryReader br = new BinaryReader(new FileStream(filePath, FileMode.Open));

                                while (br.BaseStream.Position != br.BaseStream.Length)
                                {
                                    testWeightCoef.values[index++] = br.ReadDouble();
                                }
                            }
                            catch (IOException e)
                            {
                                Logger.Info(this, "\n Cannot open Weight Coefficient file.");
                                return;
                            }

                            modeState = PredictionStates.Remote_Display;
                            break;

                        /*
                        case PredictionStates.Read_Command:
                            // There are no script file commands used in Prediction mode.
                            break;
                        */

                        case PredictionStates.Remote_Display:
                            // Do what is necessary so that current displayed BCI app page is a transparent window
                            // in order to view displayed screen of other application (e.g. Empower).

                            modeState = PredictionStates.Get_Remote_Elements;
                            break;

                        case PredictionStates.Get_Remote_Elements:
                            // Call Microsoft UIA to fetch list of GUI elements seen on display generated by another app.
                            // Create list of generic elements by extracting only those properties necessary for flashing.
                            //RemoteDisplay remoteDisplay = new RemoteDisplay();

                            elementList = remoteDisplay.GetRemoteDisplayedElements();

                            if (elementList.Count == 0)
                            {
                                // Sometimes MUIA does not find the children elements of the desktop (root) element on the first try.
                                if (numGetElementRetries == 0)
                                {
                                    // Try inserting a small delay before attempting to fetch elements again
                                    await Task.Delay(200);

                                    // Try fetching the list one more time
                                    numGetElementRetries++;
                                    modeState = PredictionStates.Get_Remote_Elements;
                                }
                                else
                                {
                                    logString = "Failed to fetch displayed elements using Microsoft UAI ... \n";
                                    Logger.Info(this, logString);
                                    modeState = PredictionStates.Clean_Up;
                                }
                            }
                            else
                            {
                                // Extract boundary rectangle information of remote display.
                                DisplayProperties displayProperties = remoteDisplay.GetDisplayProperties();

                                // Create transparent screen overlay that is at same location and size as remote display.
                                // For each element, create a button that is the same size and location as seen on the display
                                // and add it to the screen overlay. Instead of sending MUIA commands to each individual element
                                // to flash them ON/OFF, make the visual changes to the buttons in the overlay and issue a
                                // single command to update the appearance of all the buttons at the same time.
                                stimulusOverlay = flashStimulus.CreateScreenOverlay(elementList, displayProperties);

                                // Update UI with ScreenOverlay
                                if (notifyScreenOverLayChange != null) notifyScreenOverLayChange(stimulusOverlay);

                                modeState = PredictionStates.Assign_Key_Mapping;
                            }
                            if (_forceExit)
                            {
                                modeState = PredictionStates.Clean_Up;
                            }
                            break;

                        case PredictionStates.Local_Display:
                            // Have BCI app generate calibration display using URI in the DISPLAY command.
                            // This method allows creating default keyboards used in calibration and validation
                            // when using the BCI with non PRC applications.
                            // TO BE DONE: This functionality will be added after MVP

                            modeState = PredictionStates.Get_Local_Elements;
                            if (_forceExit)
                            {
                                modeState = PredictionStates.Clean_Up;
                            }
                            break;

                        case PredictionStates.Get_Local_Elements:
                            // Call function to fetch list of GUI elements seen on display generated by BCI app.
                            // TO BE DONE: This functionality will be added after MVP

                            // Ready to fetch next target from calibration commands
                            modeState = PredictionStates.Assign_Key_Mapping;
                            if (_forceExit)
                            {
                                modeState = PredictionStates.Clean_Up;
                            }
                            break;

                        case PredictionStates.Assign_Key_Mapping:
                            // Need to identify which keys (elements on display) are adjacent to one another.
                            // Construct an artificial keyboard grid and assign each element to a list of rows and columns.
                            // Using the row and column position on the grid, assign each key a unique keyID for flashing.
                            Keyboard keyboardGrid = new Keyboard();

                            gridData = keyboardGrid.ExtractGridData(elementList);

                            List<List<int>> listOfRows = keyboardGrid.CreateListOfRows(elementList);
                            List<List<int>> listOfColumns = keyboardGrid.CreateListOfColumns(elementList);

                            // If not in PRC_mode, use size of scanned list of elements for max number of rows and columns in grid.
                            if (PRC_mode == false)
                            {
                                gridData.maxKeyRows = listOfRows.Count;
                                gridData.maxKeyCols = listOfColumns.Count;
                            }

                            // Create a KeyID map of all the keys based on the mapping method and grid size
                            keyIDMap = keyboardGrid.CreateKeyIDMap(gridData, PRC_mode);

                            flashableKeys = keyboardGrid.AssignKeyMapping(elementList, gridData, keyIDMap);

                            // Create cross reference lookup table between the keyID value and buttonIndex value used in flashing keys.
                            flashStimulus.CreateCrossReferenceTable(flashableKeys);

                            modeState = PredictionStates.Create_Grouping;
                            if (_forceExit)
                            {
                                modeState = PredictionStates.Clean_Up;
                            }
                            break;

                        case PredictionStates.Create_Grouping:
                            // Run algorithms to randomize the assignment of flashable keys into two hidden matrices.
                            FlashGrouping flashGroups = new FlashGrouping();

                            // Most of the time the grouping matrix is generated using a Magic Square based algorithm
                            // to randomize the keys into flash stimulus groups. However, when the number of active 
                            // keys (or menu choices) is less than 9, then an alternative method is used to create 
                            // a flashing stimulus where only one key (or choice) is flashed at a time in a group.

                            if (flashableKeys.Count <= FlashGrouping.ACTIVE_KEYS_LOWER_LIMIT)
                            {
                                groupingMatrix = flashGroups.GenerateAlternateGroupingMaxtrix(flashableKeys, out matrixSize1, out matrixSize2);
                            }
                            else
                            {
                                groupingMatrix = flashGroups.GenerateGroupingMaxtrix(inputParam, keyIDMap, flashableKeys, gridData, out matrixSize1, out matrixSize2);
                            }

                            modeState = PredictionStates.Create_Sequence;
                            if (_forceExit)
                            {
                                modeState = PredictionStates.Clean_Up;
                            }
                            break;

                        case PredictionStates.Create_Sequence:
                            // Convert rows and columns of hidden matrices into flash groups.
                            // Create flash sequence table of flash groupings.
                            FlashSequence flashSequence = new FlashSequence();

                            flashSequenceTable = flashSequence.GenerateFlashSequenceTable(groupingMatrix, matrixSize1, matrixSize2, targetkeyIDvalue);

                            modeState = PredictionStates.Core_Loop;
                            if (_forceExit)
                            {
                                modeState = PredictionStates.Clean_Up;
                            }
                            break;

                        /*
                        case PredictionStates.Highlight_Target:
                            // There is no Highlight_Target or Unhighlight_Target state in Prediction mode
                            break;
                        */


                        /* ********************************* */
                        /*  Real-time Processing Core Loop   */
                        /* ********************************* */

                        case PredictionStates.Core_Loop:

                            bool exitFlag = false;
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

                            /* ********************************* */
                            /*
                            if (calibrationFileMetadata ==null)
                            {
                                Logger.Error(this, "calibration file meta data is not available.unable to proceed");
                                modeState = PredictionStates.Clean_Up; break;
                            }    
                           
                            // Don't need binaryWriter for Prediction to work. JAZ 4-12-24
                            binaryWriter = FileHandlerService.CreateAndOpenBinaryFile(calibrationFileMetadata.GetCurrentBinFileName(), "CalibrationFiles");
                            if (binaryWriter == null)
                            {
                                Logger.Error(this, "unable to create binary writer.");
                                modeState = PredictionStates.Clean_Up; break;
                            }
                            */
                            /* ********************************* */


                            CancellationTokenSource pipelineTokenSource = new CancellationTokenSource();

                            DataflowBlockOptions blockOptions = new DataflowBlockOptions();
                            blockOptions.CancellationToken = pipelineTokenSource.Token;

                            // Create instances of BufferBlocks to pass data between pipelined tasks.
                            var flashMsgBufferBlock = new BufferBlock<int[]>(blockOptions);
                            var flashDataBufferBlock = new BufferBlock<double[]>(blockOptions);
                            var filterDataBufferBlock = new BufferBlock<double[]>(blockOptions);

                            var weightCoefBufferBlock = new BufferBlock<WeightCoefficients>(blockOptions);
                            var commandMsgBufferBlock = new BufferBlock<int[]>(blockOptions);
                            var statusMsgBufferBlock = new BufferBlock<PredictStatusMessage>(blockOptions);

                            int[] flashControlMessage = new int[MAX_FLASH_MSG_LENGTH];

                            PredictStatusMessage predictStatusMessage = new PredictStatusMessage();

                            // Create instances of classes that comprise the processing pipeline tasks
                            // HeadsetControl headsetControl = new HeadsetControl();
                            FlashTiming flashTiming = new FlashTiming();
                            SensorFiltering sensorFiltering = new SensorFiltering();
                            PredictionControl predictionControl = new PredictionControl();
                            //RecordBinaryData recordBinary = new RecordBinaryData();

                            // Start separate tasks that fetch sensor samples from headset while flashing keys on display screen,
                            // filtering the data for each sensor channel, and recording the filtered sensor data.

                            // Task t0 = Task.Run(() => headsetControl.ReadHeadsetDataAsync(headset, headsetTokenSource.Token, headsetDataBufferBlock, headsetMsgBufferBlock));

                            Task t1 = Task.Run(() => flashTiming.FlashDataAsync(headset, pipelineTokenSource.Token, flashSequenceTable,
                                                        headsetDataBufferBlock, flashDataBufferBlock, flashMsgBufferBlock));

                            Task t2 = Task.Run(() => sensorFiltering.FilterDataAsync(pipelineTokenSource.Token, flashDataBufferBlock, filterDataBufferBlock));

                            //Task t3 = Task.Run(() => recordBinary.RecordDataAsync(pipelineTokenSource.Token, filterDataBufferBlock, binaryWriter));

                            Task t4 = Task.Run(() => predictionControl.PredictionControlAsync(pipelineTokenSource.Token, predictionConfiguration,
                                                        weightCoefBufferBlock, filterDataBufferBlock, commandMsgBufferBlock, statusMsgBufferBlock));


                            // Enable the flow of sensor data from the headset to the processing pipeline now.
                            headsetControlMessage.content[0] = ENABLE_PIPELINE_SENSOR_DATA;     // message ID
                            headsetControlMessage.content[1] = 2;                               //message length

                            // Send command message to the headset task.
                            headsetMsgBufferBlock.Post(headsetControlMessage);

                            /* ********************************* */

                            while (exitFlag == false)
                            {
                                flashControlMessage = await flashTiming.GetFlashControlMessageAsync(flashMsgBufferBlock);

                                if (_forceExit)
                                {
                                    exitFlag = true;
                                }
                                string flashTime = DateTime.Now.ToString("HH:mm:ss:ffff");

                                messageType = flashControlMessage[0];

                                if (messageType == FLASH_ON)
                                {
                                    indexValue = flashControlMessage[1] - 1;
                                    phaseInSequence = flashControlMessage[2];
                                    elapsedTime = flashControlMessage[3];

                                    flashStimulus.FlashStimulusON(targetkeyIDvalue, indexValue, flashSequenceTable);

                                    sourceTimer = elapsedTime % 65536;
                                    //Console.WriteLine("{0}  Flash ON,  seq num = {1},  phase = {2},   time = {3} \n", flashTime, indexValue, phaseInSequence, sourceTimer);
                                    //Debug.WriteLine("{0}  Flash ON,  seq num = {1},  phase = {2},   time = {3} \n", flashTime, indexValue, phaseInSequence, sourceTimer);
                                }
                                else if (messageType == FLASH_OFF)
                                {
                                    indexValue = flashControlMessage[1] - 1;
                                    phaseInSequence = flashControlMessage[2];
                                    elapsedTime = flashControlMessage[3];

                                    flashStimulus.FlashStimulusOFF(targetkeyIDvalue, indexValue, flashSequenceTable);

                                    sourceTimer = elapsedTime % 65536;
                                    //Console.WriteLine("{0}  Flash OFF,  seq num = {1},  phase = {2},   time = {3}  \n", flashTime, indexValue, phaseInSequence, sourceTimer);
                                    //Debug.WriteLine("{0}  Flash OFF,  seq num = {1},  phase = {2},   time = {3}  \n", flashTime, indexValue, phaseInSequence, sourceTimer);
                                }
                                else if (messageType != END_SEQUENCE)
                                {
                                    Logger.Info(this, flashTime + "FlashControlAsync() returned invalid message!!!  messageType = " + messageType);
                                    exitFlag = true;
                                }

                                /* ********************************* */

                                // Next poll for any status or debug messaged sent from the Prediction task...
                                predictStatusReceived = statusMsgBufferBlock.TryReceive(out predictStatusMessage);

                                if (predictStatusReceived == true)
                                {
                                    predictStatusReceived = false;

                                    messageType = predictStatusMessage.content[0];

                                    if (messageType == 18)
                                    {
                                        // MARK_STATE_CHANGE
                                        messageLen = predictStatusMessage.content[1];
                                        phaseInSequence = predictStatusMessage.content[2];
                                        stimCode = predictStatusMessage.content[3];
                                        reportedAction = predictStatusMessage.content[4];

                                        //Debug.WriteLine("{0}  MARK_STATE,  seq num = {1},  phase = {2},   time = {3} \n", flashTime, stimCode, phaseInSequence, sourceTimer);
                                        //Debug.WriteLine("{0}  MARK_STATE,  phase = {1}, seq num = {2}, action = {3} \n", flashTime, phaseInSequence, stimCode, reportedAction);

                                        if (phaseInSequence == 1)
                                        {
                                            weightCoefBufferBlock.Post(testWeightCoef);
                                        }
                                    }

                                    else if (messageType == 20)
                                    {
                                        // MARK_STIM_CHANGE
                                        messageLen = predictStatusMessage.content[1];
                                        phaseInSequence = predictStatusMessage.content[2];
                                        stimCode = predictStatusMessage.content[3];
                                        reportedAction = predictStatusMessage.content[4];

                                        //Debug.WriteLine("{0}  MARK_STIM,   seq num = {1},  phase = {2},   time = {3} \n", flashTime, stimCode, phaseInSequence, sourceTimer);
                                        //Debug.WriteLine("{0}  MARK_STIM,  phase = {1}, seq num = {2}, action = {3} \n", flashTime, phaseInSequence, stimCode, reportedAction);
                                    }
                                    
                                    else if (messageType == 21)
                                    {
                                        // PREDICT_STATUS
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

                                        //Logger.Info(this, "fseq = " + fseq + " code1 = " + code1 + " code2 = " + code2 + " code3 = " + code3 + " code4 = " + code4);
                                        //Logger.Info(this, " code5 = " + code5 + " predKey = " + predKey + " winct = " + winct + " selKey = " + selKey + "\n");
                                        //Debug.WriteLine("{0}  STATUS,  fseq = {1}, code1 = {2}, code2 = {3}, code3 = {4}, code4 = {5}, code5 = {6} \n", flashTime, fseq, code1, code2, code3, code4, code5);

                                        //Debug.WriteLine("{0}  STATUS,  fseq = {1}, code1 = {2}, code2 = {3}, code3 = {4}, code4 = {5}, code5 = {6}, \n", flashTime, fseq, code1, code2, code3, code4, code5);
                                        //Debug.WriteLine("              score1 = {0}, score2 = {1}, score3 = {2}, score4 = {3}, score5 = {4}, predKey = {5}, winCnt = {6}, selKey = {7}  \n", score1, score2, score3, score4, score5, predKey, winCnt, selKey);

                                        //Logger.Info("STATUS,  fseq = " + fseq + " code1 = " + code1 + " code2 = " + code2 + " code3 = " + code3 + " code4 = " + code4 + " code5 = " + code5 + " iPoint1 = " + iPoint1 + " iPoint2 = " + iPoint2 + "\n");
                                        //Logger.Info("         score1 = " + score1 + " score2 = " + score2 + " score3 = " + score3 + " score4 = " + score4 + " score5 = " + score5 + " predKey = " + predKey + " winCnt = " + winCnt + " selKey = " + selKey + "\n");

                                        Logger.Info("STATUS,  fseq = " + fseq + "  code1 = " + code1 + "   code2 = " + code2 + "      code3 = " + code3 + "       code4 = " + code4 + "       code5 = " + code5 + "      iPoint1 = " + iPoint1 + "  iPoint2 = " + iPoint2);
                                        Logger.Info("                score1 = " + score1 + "  score2 = " + score2 + "   score3 = " + score3 + "   score4 = " + score4 + "  score5 = " + score5 + "   predKey = " + predKey + "  winCnt = " + winCnt + "  selKey = " + selKey + "\n");

                                    }

                                    else if (messageType == 99)
                                    {
                                        // END_OF_PREDICTION
                                        exitFlag = true;

                                        messageLen = predictStatusMessage.content[1];
                                        fseq = predictStatusMessage.content[2];
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

                                        // We have a winner if Prediction task returns nonzero value for selectedKeyIDvalue !!!
                                        // If value is zero, then Prediction could not be made within a preset number of flash sequences,
                                        // i.e. the sensor signals did not reach the preset config values set for detection criteria.
                                        selectedKeyIDvalue = selKey;

                                        //Logger.Info(this, "fseq = " + fseq + " code1 = " + code1 + " code2 = " + code2 + "code3 = " + code3 + " code4 = " + code4);
                                        //Logger.Info(this, " code5 = " + code5 + "  predKey " + predKey + " winCnt = " + winCnt + "selKey = " + selKey + "\n");

                                        //Debug.WriteLine("{0}  STATUS,  fseq = {1}, code1 = {2}, code2 = {3}, code3 = {4}, code4 = {5}, code5 = {6}, \n", flashTime, fseq, code1, code2, code3, code4, code5);
                                        //Debug.WriteLine("              score1 = {0}, score2 = {1}, score3 = {2}, score4 = {3}, score5 = {4}, predKey = {5}, winCnt = {6}, selKey = {7}  \n", score1, score2, score3, score4, score5, predKey, winCnt, selKey);

                                        //Logger.Info("STATUS,  fseq = " + fseq + " code1 = " + code1 + " code2 = " + code2 + " code3 = " + code3 + " code4 = " + code4 + " code5 = " + code5 + " iPoint1 = " + iPoint1 + " iPoint2 = " + iPoint2 + "\n");
                                        //Logger.Info("         score1 = " + score1 + " score2 = " + score2 + " score3 = " + score3 + " score4 = " + score4 + " score5 = " + score5 + " predKey = " + predKey + " winCnt = " + winCnt + " selKey = " + selKey + "\n");

                                        Logger.Info("SELECT,  fseq = " + fseq + "  code1 = " + code1 + "   code2 = " + code2 + "      code3 = " + code3 + "       code4 = " + code4 + "       code5 = " + code5 + "      iPoint1 = " + iPoint1 + "  iPoint2 = " + iPoint2);
                                        Logger.Info("                score1 = " + score1 + "  score2 = " + score2 + "   score3 = " + score3 + "   score4 = " + score4 + "  score5 = " + score5 + "   predKey = " + predKey + "  winCnt = " + winCnt + "  selKey = " + selKey + "\n");
                                        Logger.Info("\n\n\n ************************ \n\n");

                                    }

                                }  // if (predictStatusReceived == true)

                            }  // while (exitFlag == false)

                            /* +++++++++++++++++++++++++++++++++++++++ */

                            ++numTargets;

                            // Disable the flow of sensor data from the headset to the processing pipeline now.
                            headsetControlMessage.content[0] = DISABLE_PIPELINE_SENSOR_DATA;     // message ID
                            headsetControlMessage.content[1] = 2;                               //message length

                            // Send command message to the headset task.
                            headsetMsgBufferBlock.Post(headsetControlMessage);

                            await Task.Delay(100);

                            // Stop processing pipeline tasks by cancelling the shared token
                            pipelineTokenSource.Cancel();

                            await Task.Delay(100);

                            filterDataBufferBlock.Complete();
                            flashDataBufferBlock.Complete();
                            flashMsgBufferBlock.Complete();

                            weightCoefBufferBlock.Complete();
                            commandMsgBufferBlock.Complete();
                            statusMsgBufferBlock.Complete();

                            //await Task.Delay(100);
                            //Logger.Info(this, "Status of Task t0: " + t0.Status);
                            //Logger.Info(this, "Status of Task t1: " + t1.Status);
                            //Logger.Info(this, "Status of Task t2: " + t2.Status);
                            //Logger.Info(this, "Status of task t4: " + t4.Status);
                            if (_forceExit)
                            {
                                modeState = PredictionStates.Clean_Up;
                                break;
                            }
                            modeState = PredictionStates.Click_Key;      // Don't click key until Prediction is working
                            // modeState = PredictionStates.Programmed_Delay;
                            break;

                        /* ********************************* */
                        /*  End Core Loop                    */
                        /* ********************************* */
                           

                        case PredictionStates.Click_Key:
                            // Clicking target key generates speech (in REMOTE) and displays key name.
                            // flashableKeys[] contains the mapping between the clickable elements seen on the display with
                            // the flashable elements created in the artificial screen overlay. Find the index location in
                            // flashableKeys[] with a keyID matching the selKey, then use either the corresponding
                            // elementIndex to access the list of clickable GUI elements on the screen, or the buttonIndex
                            // to flash the corresponding button in the screen overlay.

                            if (selectedKeyIDvalue == 0)
                            {
                                // Prediction failed to detect user key selection within given time limit.
                                // Record message in log for now, and simply try again one more time.

                                logString = " ++++++>  WARNING: Prediction could not detect user key selection within time limit!  <++++++ \n";
                                Logger.Info(this, logString);
                            }
                            else
                            {
                                // Determine which key was selected on the display.
                                elementIndex = 0;
                                for (int i = 0; i < flashableKeys.Count; i++)
                                {
                                    if (flashableKeys[i].keyID == selectedKeyIDvalue)
                                    {
                                        elementIndex = flashableKeys[i].elementIndex;
                                        break;
                                    }
                                }

                                if (elementIndex == 0)
                                {
                                    // There is a mapping problem between the list of elements on the display and
                                    // the key ID number assigned to the overlayed keyboard for flash grouping.
                                    logString = "Matching selectedKeyIDvalue NOT FOUND!!! = " + selectedKeyIDvalue.ToString() + "\n";
                                    Logger.Info(this, logString);
                                }
                                else
                                {
                                    logString = "  ******  selectedKeyIDvalue  = " + selectedKeyIDvalue.ToString() + "\n";
                                    Logger.Info(this, logString);

                                    // It's time to click on the key!
                                    remoteDisplay.SelectElement(elementIndex);
                                }
                            }
                            if (_forceExit)
                            {
                                modeState = PredictionStates.Clean_Up;
                                break;
                            }
                            modeState = PredictionStates.Programmed_Delay;
                            break;

                        case PredictionStates.Programmed_Delay:
                            // Wait for programmable delay so Empower can complete its click repsonse.
                            // Need to wait for Empower to generate speech, display key name in text box,
                            // and possibly update keyboard display with new set of keys.
                            await Task.Delay(programmable_delay_msec);

                            // Fetch next target in calibration set
                            modeState = PredictionStates.Monitor_Prediction_Interfaces;
                            if (_forceExit)
                            {
                                modeState = PredictionStates.Clean_Up;
                                break;
                            }
                            break;

                        case PredictionStates.Monitor_Prediction_Interfaces:
                            // For MVP we just need a mechanism to stop Prediction mode and return to the BCI Main Menu.
                            // The basic goal of this state is to check for the exit condition. If the exit condition
                            // was not selected, then continue predictiong the next key selection.

                            // if (exitCondition == true)
                            //    modeState = PredictionStates.Clean_Up;
                            // else
                            //    modeState = PredictionStates.Get_Remote_Elements;

                            // modeState = PredictionStates.Clean_Up;               // <-- Use this to run once, then stop

                            modeState = PredictionStates.Get_Remote_Elements;  
                            // <-- Use this to run forever...
                            break;

                        /*
                        case PredictionStates.Stop_Message:
                            //Send UI message to show Stop query
                            NotifyUI(new UIInstruction { Type = UIType.PredictionStopQuery });
                            modeState = PredictionStates.Wait_On_UI;
                            break;

                        case PredictionStates.Ready_To_Begin:
                            modeState = PredictionStates.Wait_On_UI;
                            NotifyUI(new UIInstruction { Type = UIType.PredictionBeginQuery });
                            break;
                        */

                        case PredictionStates.Clean_Up:
                            // Close all open files and delete class objects (except headset)
                            modeState = PredictionStates.Exit_Mode;
                            break;

                        case PredictionStates.Exit_Mode:
                            // Now ready to return to the BCI app Main Menu
                            modeExit = true;
                            headsetTokenSource.Cancel();
                            headset.StopBackgroundAcquisition();
                            await Task.Delay(50);

                            headsetMsgBufferBlock.Complete();
                            headsetDataBufferBlock.Complete();

                            headset.Disconnect();
                            headset.Delete();
                            await Task.Delay(1000);
                            if (exitAction != null) exitAction();
                            break;

                    }  // switch (modeState)

                }  // while (!modeExit)

                // Stop headset task by cancelling the token
                if (headset.IsConnected())
                {
                    headsetTokenSource.Cancel();
                    headset.StopBackgroundAcquisition();
                    await Task.Delay(50);

                    headsetMsgBufferBlock.Complete();
                    headsetDataBufferBlock.Complete();

                    headset.Disconnect();
                    headset.Delete();
                    await Task.Delay(100);
                }

                /*
                // Don't need binaryWriter for Prediction to work. JAZ 4-12-24
                if (binaryWriter != null)
                {
                    FileHandlerService.CloseBinaryWriter(binaryWriter);
                }
                */

                /*
                if(calibrationCompleted&&completeAction!=null)
                {
                    completeAction();
                }
                */

            }
            catch (Exception ex)
            {
                Logger.Error(this, "Exception in state engine" + ex.ToString());
            }
        }

        /* ++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++ */
        /* ++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++ */

        public void NotificationfromUI(UIInstruction instruction)
        {
            //if (string.IsNullOrEmpty(instruction)) return;
            if(modeState== PredictionStates.Wait_On_UI)
            {
                uiInstruction = instruction;
                modeState = PredictionStates.Process_UI_Notification;
            }

        }
        public bool CheckEmpowerIsAlive()
        {
            return remoteDisplay.CheckEmpowerIsAlive();
        }

        private void NotifyUI(UIInstruction uiInstruction)
        {
            if (predictionFileMetadata != null) uiInstruction.Param = predictionFileMetadata;
            if(notifyUIAction!=null)
            {
                notifyUIAction(uiInstruction);
            }
        }

        public void SetPredictionFile(PredictionFileMetadata predictionFileMetadata)
        {
            this.predictionFileMetadata = predictionFileMetadata;
        }
        public void ForceExit()
        {
            _forceExit = true;
        }
        public void UnsetExitFlag()
        {
            _forceExit = false;
        }
        /* ++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++ */
        /* ++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++ */

    }
}
