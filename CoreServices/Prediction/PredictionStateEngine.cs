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

        private CalibrationFileMetadata calibrationFileMetadata;    // Still need ???

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
            "Clean_Up", "Exit_Mode", "Notify_UI", "Wait_On_UI", "Process_UI_Notification"
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

            bool calibrationCompleted = false;

            //int startSetNum = 1;
            //int totalSetNum = 0;
            //int currentSetNum = 0;
            //int targetsReadFromSet = 0;

            int numGetElementRetries = 0;
            int repeatCalibrationLine = 0;      // Still need ???
            int currentLine = 0;
            int maxLinesInFile = 0;
            int targetkeyIDvalue = 0;
            int matrixSize1 = 0;
            int matrixSize2 = 0;
            bool showCountDownTimer = true;

            int elementIndex = 0;

            int UI_delay_msec = 200;
            int programmable_delay_msec = 1000;

            string targetKeyName;
            string[] commandLines = null;

            bool modeExit = false;
            bool foundMatch = false;

            const int MAX_MSG_LENGTH = 8;

            const int FLASH_ON = 1;
            const int FLASH_OFF = 2;
            const int END_SEQUENCE = 3;

            const int samplesPerBatch = 9;              // 9 samples = 30 msec @ Fs=300 Hz
            const double bufferAheadSec = 0.100;        // 100 msec

            int numTargets = 1;

            int messageType;
            int indexValue;
            int phaseInSequence;
            int elapsedTime;
            int sourceTimer;

            int[] messageBuffer = new int[MAX_MSG_LENGTH];

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

            PredictionPresets predictionConfiguration = new PredictionPresets();


            /*   *****************************
            **   ***  Instantiate classes  ***   
            **   *****************************
            */
            string currentDirectory = Directory.GetCurrentDirectory();

            //string scriptFilename = "CalibrationCommands.txt";
            //string filePath = System.IO.Path.Combine(currentDirectory, scriptFilename);

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
                headset.ConnectPort(port);
                await Task.Delay(100);

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

            try
            {
                BinaryWriter? binaryWriter = null;

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
                            break;

                        case PredictionStates.Local_Display:
                            // Have BCI app generate calibration display using URI in the DISPLAY command.
                            // This method allows creating default keyboards used in calibration and validation
                            // when using the BCI with non PRC applications.
                            // TO BE DONE: This functionality will be added after MVP

                            modeState = PredictionStates.Get_Local_Elements;
                            break;

                        case PredictionStates.Get_Local_Elements:
                            // Call function to fetch list of GUI elements seen on display generated by BCI app.
                            // TO BE DONE: This functionality will be added after MVP

                            // Ready to fetch next target from calibration commands
                            modeState = PredictionStates.Assign_Key_Mapping;
                            break;

                        case PredictionStates.Assign_Key_Mapping:
                            // Need to identify which keys (elements on display) are adjacent to one another.
                            // Construct an artificial keyboard grid and assign each element to a list of rows and columns.
                            // Using the row and column position on the grid, assign each key a unique keyID for flashing.
                            Keyboard keyboardGrid = new Keyboard();

                            gridData = keyboardGrid.ExtractGridData(elementList);

                            List<List<int>> listOfRows = keyboardGrid.CreateListOfRows(elementList);
                            List<List<int>> listOfColumns = keyboardGrid.CreateListOfColumns(elementList);
                            gridData.maxKeyRows = listOfRows.Count;
                            gridData.maxKeyCols = listOfColumns.Count;

                            bool PRC_mode = true;

                            // Create a KeyID map of all the keys based on the mapping method and grid size
                            keyIDMap = keyboardGrid.CreateKeyIDMap(gridData, PRC_mode);

                            flashableKeys = keyboardGrid.AssignKeyMapping(elementList, gridData, keyIDMap);

                            // Create cross reference lookup table between the keyID value and buttonIndex value used in flashing keys.
                            flashStimulus.CreateCrossReferenceTable(flashableKeys);

                            modeState = PredictionStates.Create_Grouping;
                            break;

                        case PredictionStates.Create_Grouping:
                            // Run algorithms to randomize the assignment of flashable keys into two hidden matrices.
                            FlashGrouping flashGroups = new FlashGrouping();

                            groupingMatrix = flashGroups.GenerateGroupingMaxtrix(inputParam, keyIDMap, flashableKeys, gridData, out matrixSize1, out matrixSize2);

                            modeState = PredictionStates.Create_Sequence;
                            break;

                        case PredictionStates.Create_Sequence:
                            // Convert rows and columns of hidden matrices into flash groups.
                            // Create flash sequence table of flash groupings.
                            FlashSequence flashSequence = new FlashSequence();

                            flashSequenceTable = flashSequence.GenerateFlashSequenceTable(groupingMatrix, matrixSize1, matrixSize2, targetkeyIDvalue);

                            modeState = PredictionStates.Core_Loop;
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

                            headset.StartBackgroundAcquisition();

                            CancellationTokenSource tokenSource = new CancellationTokenSource();

                            // Create instances of BufferBlocks to pass data between pipelined tasks.
                            var messageBufferBlock = new BufferBlock<int[]>();
                            var dataBufferBlock    = new BufferBlock<double[]>();
                            var filterBufferBlock  = new BufferBlock<double[]>();
                            var weightCoefBufferBlock = new BufferBlock<double[]>(); 
                            //var packetDataBufferBlock = new BufferBlock<double[]>();
                            var commandMsgBufferBlock = new BufferBlock<int[]>();
                            var statusMsgBufferBlock  = new BufferBlock<int[]>();

                            // Create instances of classed that comprise the processing pipeline tasks
                            FlashTiming flashTiming = new FlashTiming();
                            SensorFiltering sensorFiltering = new SensorFiltering();
                            PredictionControl predictionControl = new PredictionControl();
                            RecordBinaryData recordBinary = new RecordBinaryData();

                            //if (calibrationFileMetadata ==null)
                            //{
                            //    Logger.Error(this, "calibration file meta data is not available.unable to proceed");
                            //    modeState = PredictionStates.Clean_Up; break;
                            //}    
                           
                            //binaryWriter = FileHandlerService.CreateAndOpenBinaryFile(calibrationFileMetadata.GetCurrentBinFileName(), "CalibrationFiles");
                            //if (binaryWriter == null)
                            //{
                            //    Logger.Error(this, "unable to create binary writer.");
                            //    modeState = PredictionStates.Clean_Up; break;
                            //}
                            // Start separate tasks that fetch sensor samples from headset while flashing keys on display screen,
                            // filtering the data for each sensor channel, and recording the filtered sensor data.
                            Task t1 = Task.Run(() => flashTiming.FlashDataAsync(headset, tokenSource.Token, flashSequenceTable, 
                                                        messageBufferBlock, dataBufferBlock));

                            Task t2 = Task.Run(() => sensorFiltering.FilterDataAsync(tokenSource.Token, dataBufferBlock, filterBufferBlock));

                            //Task t3 = Task.Run(() => recordBinary.RecordDataAsync(tokenSource.Token, filterBufferBlock, binaryWriter));

                            Task t4 = Task.Run(() => predictionControl.PredictionControlAsync(tokenSource.Token, predictionConfiguration,
                                                        weightCoefBufferBlock, filterBufferBlock, commandMsgBufferBlock, statusMsgBufferBlock));


                            while (exitFlag == false)
                            {
                                messageBuffer = await flashTiming.GetFlashControlMessageAsync(messageBufferBlock);

                                string flashTime = DateTime.Now.ToString("HH:mm:ss:ffff");

                                messageType = messageBuffer[0];

                                if (messageType == FLASH_ON)
                                {
                                    indexValue = messageBuffer[1] - 1;
                                    phaseInSequence = messageBuffer[2];
                                    elapsedTime = messageBuffer[3];

                                    flashStimulus.FlashStimulusON(targetkeyIDvalue, indexValue, flashSequenceTable);

                                    sourceTimer = elapsedTime % 65536;
                                    //Console.WriteLine("{0}  Flash ON,  seq num = {1},  phase = {2},   time = {3} \n", flashTime, indexValue, phaseInSequence, sourceTimer);
                                }
                                else if (messageType == FLASH_OFF)
                                {
                                    indexValue = messageBuffer[1] - 1;
                                    phaseInSequence = messageBuffer[2];
                                    elapsedTime = messageBuffer[3];

                                    flashStimulus.FlashStimulusOFF(targetkeyIDvalue, indexValue, flashSequenceTable);

                                    sourceTimer = elapsedTime % 65536;
                                    //Console.WriteLine("{0}  Flash OFF,  seq num = {1},  phase = {2},   time = {3}  \n", flashTime, indexValue, phaseInSequence, sourceTimer);
                                }
                                else if (messageType == END_SEQUENCE)
                                {
                                    exitFlag = true;
                                }
                                else
                                {
                                    Logger.Info(this, flashTime + "FlashControlAsync() returned invalid message!!!  messageType = "+ messageType);
                                    exitFlag = true;
                                }

                            }  // while (exitFlag == false)

                            ++numTargets;

                            // Stop task that is fetching sensor samples from headset
                            tokenSource.Cancel();

                            //await Task.Delay(50);
                            headset.StopBackgroundAcquisition();
                            headset.Idle(0.200);

                            messageBufferBlock.Complete();
                            dataBufferBlock.Complete();

                            modeState = PredictionStates.Click_Key;
                            break;

                        /* ********************************* */
                        /*  End Core Loop                    */
                        /* ********************************* */


                        case PredictionStates.Click_Key:
                            // Clicking target key generates speech (in REMOTE) and displays key name.
                            // flashableKeys[] contains the mapping between the clickable elements seen on the display with
                            // the flashable elements created in the artificial screen overlay. Find the index location in
                            // flashableKeys[] with a keyID matching the targetkeyIDvalue, then use either the corresponding
                            // elementIndex to access the list of clickable GUI elements on the screen, or the buttonIndex
                            // to flash the corresponding button in the screen overlay.

                            elementIndex = 0;
                            for (int i = 0; i < flashableKeys.Count; i++)
                            {
                                if (flashableKeys[i].keyID == targetkeyIDvalue)
                                {
                                    elementIndex = flashableKeys[i].elementIndex;
                                    break;
                                }
                            }

							logString = "  ******  targetkeyIDvalue  = " + targetkeyIDvalue.ToString() + "\n";
                            Logger.Info(this, logString);

                            if (elementIndex == 0)
                            {
                                logString = "Matching targetkeyIDvalue NOT FOUND!!! = " + targetkeyIDvalue.ToString() + "\n";
                                Logger.Info(this, logString);
                            }
                            else
                            {
                                remoteDisplay.SelectElement(elementIndex);
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
                            break;

                        case PredictionStates.Monitor_Prediction_Interfaces:
                            // For MVP we just need a mechanism to stop Prediction mode and return to the BCI Main Menu.
                            // The basic goal of this state is to check for the exit condition. If the exit condition
                            // was not selected, then continue predictiong the next key selection.

                            // if (exitCondition == true)
                            //    modeState = PredictionStates.Clean_Up;
                            // else
                            //    modeState = PredictionStates.Get_Remote_Elements;
                            modeState = PredictionStates.Clean_Up;
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
                            if (exitAction != null) exitAction();
                            break;

                    }  // switch (modeState)

                }  // while (!modeExit)

                // this.Close();

                headset.Disconnect();
                headset.Delete();

                if (binaryWriter != null)
                {
                    FileHandlerService.CloseBinaryWriter(binaryWriter);
                }

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
            if (calibrationFileMetadata != null) uiInstruction.Param = calibrationFileMetadata;
            if(notifyUIAction!=null)
            {
                notifyUIAction(uiInstruction);
            }
        }

        public void SetCalibrationFile(CalibrationFileMetadata calibrationFileMetadata)
        {
            this.calibrationFileMetadata = calibrationFileMetadata;
        }

        /* ++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++ */
        /* ++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++ */

    }
}
