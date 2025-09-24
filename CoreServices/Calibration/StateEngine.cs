using BCI_Headset;
using CoreServices.Common;
using CoreServices.Files;
using CoreServices.Models;
using CoreServices.Stimulus;
using CoreServices.FlashControl;
using CoreServices.Filtering;
using CoreServices.Recording;

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Threading.Tasks.Dataflow;
using System.Reflection;
using System.Diagnostics;

using CoreServices.Headset;
using CoreServices.Presets;

namespace CoreServices.Calibration
{
    public class StateEngine
    {
        private RemoteDisplay remoteDisplay = new RemoteDisplay();
        private FlashStimulus flashStimulus = new FlashStimulus();
        private CalibrationStates modeState;
        private UIInstruction uiInstruction;
        private Action<UIInstruction> notifyUIAction;
        private Action activeUIAction;
        private Action<ScreenOverlay> notifyScreenOverLayChange;
        private CalibrationFileMetadata calibrationFileMetadata;


        private Action exitAction;
        private Action completeAction;
        private Action pauseAction;

        private bool _forceExit;
        public enum CalibrationStates
        {
            Show_Instructions, Open_Files, Read_Command, Remote_Display, Get_Remote_Elements,
            Local_Display, Get_Local_Elements, Assign_Key_Mapping, Create_Grouping, Create_Sequence,
            Highlight_Target, Test_Script_Enabled, Core_Loop, Unhighlight_Target, Click_Key, Programmed_Delay,
            Clear_Textbox, Stop_Message, Ready_To_Begin, Clean_Up, Exit_Mode,Notify_UI,Wait_On_UI,Process_UI_Notification
        }

        public string[] CalibrationStateNames =
        {
            "Show_Instructions", "Open_Files", "Read_Command", "Remote_Display", "Get_Remote_Elements",
            "Local_Display", "Get_Local_Elements", "Assign_Key_Mapping", "Create_Grouping", "Create_Sequence",
            "Highlight_Target", "Test_Script_Enabled", "Core_Loop", "Unhighlight_Target", "Click_Key", "Programmed_Delay",
            "Clear_Textbox", "Stop_Message", "Ready_To_Begin", "Clean_Up", "Exit_Mode",
            "Notify_UI", "Wait_On_UI", "Process_UI_Notification"
        };

        public StateEngine(Action<UIInstruction> notifyUIAction,Action<ScreenOverlay> notifyScreenOverLayChange, Action exitAction,Action completeAction,Action pauseAction,Action activateUIAction)
        {
            this.notifyUIAction = notifyUIAction;
            this.notifyScreenOverLayChange = notifyScreenOverLayChange;
            this.exitAction = exitAction;
            this.completeAction = completeAction;
            this.pauseAction = pauseAction;
            this.activeUIAction = activateUIAction;
            _forceExit = false;
        }

        public async Task CalibrationMode()
        {
            // NOTE: The following inputs must be "passed into" the Calibration Mode:
            //
            //   int startSetNum            // Which set number to begin at in the script
            //   int flash_ON_time_msec
            //   int flash_OFF_time_msec
            //   int delayBetweenTargets_msec

            GroupingParam inputParam;
            inputParam.method = Method.NewMethod;
            inputParam.keyOrder = KeyOrder.Switchback;
            inputParam.boardDivision = BoardDivision.Checkerboard;
            inputParam.arrangement = Arrangement.Magic;
            inputParam.indexMapping = IndexMapping.GridSize;
            bool calibrationCompleted = false;

            // PRC_mode eventually needs to be a controllable preset value to support different key grids.
            bool PRC_mode = true;

            int startSetNum = 1;
            int totalSetNum = 0;
            int currentSetNum = 0;
            int targetsReadFromSet = 0;
            int numGetElementRetries = 0;
            int repeatCalibrationLine = 0;
            int currentLine = 0;
            int maxLinesInFile = 0;
            int targetkeyIDvalue = 0;
            int matrixSize1 = 0;
            int matrixSize2 = 0;
            bool showCountDownTimer = true;

            int elementIndex = 0;

            int UI_delay_msec = 200;

            string targetKeyName;
            string[] commandLines = null;

            bool modeExit = false;
            bool foundMatch = false;

            bool testScriptEnabled = false;

            const int MAX_FLASH_MSG_LENGTH = 8;

            const int FLASH_ON = 1;
            const int FLASH_OFF = 2;
            const int END_SEQUENCE = 3;

            //bool testScriptEnabled = true;
            const int ENABLE_PIPELINE_SENSOR_DATA = 1;
            const int DISABLE_PIPELINE_SENSOR_DATA = 2;
            
            const int samplesPerBatch = 9;              // 9 samples = 30 msec @ Fs=300 Hz
            const double bufferAheadSec = 0.100;        // 100 msec

            int numTargets = 1;

            int messageType;
            int indexValue;
            int phaseInSequence;
            int elapsedTime;
            int sourceTimer;

            bool randomMapping = true;
            int randomValue = 0;

            Random randomGen = new Random();

            modeState = CalibrationStates.Read_Command;


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

            // Fetch preset configuration values for flash stimulus from stored settings file.
            FlashStimulusPresets flashConfiguration = FlashStimulusPresets.Instance;

            // Convert preset values in seconds into milliseconds by multiplying by 1000.
            int timeToFindTarget = flashConfiguration.FlashTimingPresets.timeToFindTarget * 1000;
            int timeBetweenTargets = flashConfiguration.FlashTimingPresets.timeBetweenTargets * 1000;

            // Fetch preset configuration values for calibration from stored settings file.
            CalibrationPresets calibrationConfiguration = CalibrationPresets.Instance;

            // This preset controls the amount of raw sensor data being recorded for weight coefficient generation.
            int numFlashSeqPerTarget = calibrationConfiguration.ModeControlPresets.numFlashSeqPerTarget;

            string currentDirectory = Path.GetDirectoryName(Assembly.GetEntryAssembly().Location);

            //string scriptFilename = calibrationConfiguration.ModeControlPresets.scriptFilename;
            string scriptFilename = "CalibrationCommands.txt";

            string filePath = System.IO.Path.Combine(currentDirectory, scriptFilename);


            /*   *****************************
            **   ***   Configure Headset   ***   
            **   *****************************
            */


            // NOTE:  JAZ, 04-30-2024
            //
            // At some point the following headset configuration code needs some serious cleaning up.
            // Each BCI app mode *SHOULD NOT* have its own separate headset configuration! The attempted
            // fix in HeadsetServices *DID NOT* make things any better. Now we have two spots in the
            // code creating a headset instantiation. This needs to be done in one location and the
            // headset handle needs to be passed to the separate modes when they are started.
            //
            // For now *DO NOT TOUCH* any headset code to address this problem!!! Setting up and
            // configuring the headset COM port is an intertwined checken and egg type problem
            // that needs to be addressed at the same time as resolving headset configuration.
            //

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
            /*
            // Need to replace this code with try/catch block to check for file exists and can be opened.
            if (File.Exists(filePath))
            {
                commandLines = File.ReadAllLines(filePath);
                maxLinesInFile = commandLines.Length;
                currentLine = 0;
                modeState = CalibrationStates.Read_Command;
            }
            else
            {
               Logger.Info(this,filePath + " does not exist!");
                modeState = CalibrationStates.Clean_Up;
            }
            */

            // Start the headset driver at the operating system level
            headset.StartBackgroundAcquisition();

            /* ++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++ */

            // NOTE:  JAZ, 04-30-2024
            //
            // I made a critical software infrastructure change moving from this point forward!!!
            // Starting the headset data acquisition at the start of each target flash sequence
            // and then stopping the acquisition at the end of each data collection is a flaw
            // in the software design. The objective was to synchronize the start of the flashing
            // stimulus with the start of data collection. Unfortunately restarting acquistion
            // created a large noise spike at the start of each target data collection.
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
            var headsetMsgBufferBlock  = new BufferBlock<HeadsetControlMessage>();

            HeadsetControlMessage headsetControlMessage = new HeadsetControlMessage();

            Task t0 = Task.Run(() => headsetControl.ReadHeadsetDataAsync(headset, headsetTokenSource.Token, headsetDataBufferBlock, headsetMsgBufferBlock));


            /* ++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++ */

            try
            {
                commandLines = File.ReadAllLines(filePath);
                maxLinesInFile = commandLines.Length;
                currentLine = 0;
                repeatCalibrationLine = 0;
                BinaryWriter? binaryWriter=null;
                if(calibrationFileMetadata != null)
                {
                    startSetNum = calibrationFileMetadata.CurrentSet;
                }

                // Continue performing calibration mode until either:
                //   - Script runs to completion, user exits at end of calibration set, or error occurs.

                while (!modeExit)
                {
                    string logString = "currentLine = " + currentLine.ToString() + "  modeState = " + CalibrationStateNames[(int)modeState] + "\n";
                    Logger.Info(this, logString);

                    switch (modeState)
                    {
                        /* ************* */
                        /*  Outer Loop   */
                        /* ************* */

                        case CalibrationStates.Wait_On_UI:
                            do
                            {
                                await Task.Delay(UI_delay_msec);

                            } while (CalibrationStates.Wait_On_UI == modeState);
                            break;

                        case CalibrationStates.Process_UI_Notification:

                            if (uiInstruction == null) { modeState = CalibrationStates.Clean_Up; break; }

                            if (uiInstruction.Type == UIType.CalibrationStopQuery)
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
                                        if (completeAction != null)
                                        {
                                            completeAction();
                                        }
                                        uiInstruction.Result = false;
                                        NotifyUI(uiInstruction);
                                    }
                                    else
                                    {
                                        // Ask user if they want to continue with next calibration set
                                        modeState = CalibrationStates.Ready_To_Begin;
                                    }
                                }
                                else
                                {
                                    Logger.Info(this, "deleting the current calibration set file:" + calibrationFileMetadata.GetCurrentBinFileName());
                                    FileHandlerService.DeleteBinaryFile(calibrationFileMetadata.GetCurrentBinFileName(), calibrationFileMetadata.FilePath);
                                    modeState = CalibrationStates.Wait_On_UI;
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
                                    modeState = CalibrationStates.Read_Command;
                                }
                                else
                                {
                                    // Clean up and return to Calibration submenu (not main BCI menu)
                                    calibrationFileMetadata.CurrentSet = calibrationFileMetadata.CurrentSet - 1;
                                    if (pauseAction != null) pauseAction();
                                    modeState = CalibrationStates.Clean_Up;
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
                                    modeState = CalibrationStates.Read_Command;
                                }
                                else
                                {   // User selected Cancel button to halt further raw data collection and will resume later.
                                    // Clean up and return to Calibration submenu (not main BCI menu)
                                    if (pauseAction != null) pauseAction();
                                    modeState = CalibrationStates.Clean_Up;
                                }
                            }
                            else if (uiInstruction.Type == UIType.CountDownWindow)
                            {
                                if (uiInstruction.Result == true)
                                {
                                    showCountDownTimer = false;
                                    modeState = CalibrationStates.Highlight_Target;
                                }
                                else
                                {
                                    // Clean up and return to Calibration submenu (not main BCI menu)
                                    if (calibrationFileMetadata!=null && calibrationFileMetadata.CurrentSet > 1)
                                    {
                                        calibrationFileMetadata.CurrentSet = calibrationFileMetadata.CurrentSet - 1;
                                    }
                                    if (pauseAction != null) pauseAction();
                                    modeState = CalibrationStates.Clean_Up;
                                }
                            }
                            else if(uiInstruction.Type == UIType.CalibrationComplete)
                            {
                                modeState = CalibrationStates.Clean_Up;
                            }
                            break;

                        case CalibrationStates.Read_Command:
                            if(startSetNum > 5)
                            {
                                if (completeAction != null)
                                {
                                    completeAction();
                                }
                                modeState = CalibrationStates.Clean_Up;
                                break;
                            }
                            // Check if reached end of script file yet
                            if (currentLine >= maxLinesInFile)
                            {
                                modeState = CalibrationStates.Clean_Up;
                            }
                            else
                            {
                                // Read calibration command from list of strings in text file
                                string[] fields = commandLines[currentLine].Split(' ');

                                // Parse command string contents into field values and decide what to do next
                                if (fields[0] == "START")
                                {
                                    // Determine which set number you are at in the script
                                    currentSetNum = Int32.Parse(fields[1]);
                                    totalSetNum = Int32.Parse(fields[2]);
                                    calibrationFileMetadata.NumofSets = totalSetNum;
                                    calibrationFileMetadata.CurrentSet = currentSetNum;
                                    calibrationFileMetadata.IsCurrent = true;
                                    if (currentSetNum == startSetNum)
                                    {
                                        // Found where we need to start. Advance to next command in list
                                        // while keeping modeState == Read_Command
                                        currentLine++;

                                        // Save location in the script in case we need to discard data and repeat calibration set
                                        repeatCalibrationLine = currentLine;

                                        // Zero out count of targets read from set thus far
                                        targetsReadFromSet = 0;
                                    }
                                    else
                                    {
                                        // Continue readling and parsing lines until find matching set number or run out of lines
                                        foundMatch = false;

                                        while ((!foundMatch) & (currentLine < maxLinesInFile))
                                        {
                                            currentLine++;

                                            string[] nextLine = commandLines[currentLine].Split(' ');

                                            // Ignore commands until find start of next set
                                            if (nextLine[0] == "START")
                                            {
                                                currentSetNum = Int32.Parse(nextLine[1]);
                                                calibrationFileMetadata.CurrentSet = currentSetNum;
                                                if (currentSetNum == startSetNum)
                                                {
                                                    foundMatch = true;
                                                    // Found where we need to start. Advance to next command in list.
                                                    currentLine++;

                                                    // Save location in the script in case we need to discard data and repeat calibration set
                                                    repeatCalibrationLine = currentLine;
                                                }
                                            }
                                        }

                                        if (foundMatch == false)
                                        {
                                            Logger.Error(this, "ERROR: Can't find starting set number in calibration commands");
                                            modeState = CalibrationStates.Clean_Up;
                                        }
                                    }

                                }
                                else if (fields[0] == "DISPLAY")
                                {
                                    // Determine who is responsible for generating the calibration display
                                    if (fields[1] == "REMOTE")
                                    {
                                        // Current screen displayed by other app (e.g. Empower) is used for calibration
                                        modeState = CalibrationStates.Remote_Display;
                                    }
                                    else if (fields[1] == "LOCAL")
                                    {
                                        // Get path of page to display by BCI app during calibration
                                        // pageURI = fields[2]
                                        modeState = CalibrationStates.Local_Display;
                                    }
                                    else
                                    {
                                        Logger.Error(this, "ERROR: Invalid DISPLAY calibration command");
                                        modeState = CalibrationStates.Clean_Up;
                                    }

                                    // Advance index to next command in the script
                                    currentLine++;
                                }

                                else if (fields[0] == "TARGET")
                                {
                                    targetkeyIDvalue = Int32.Parse(fields[1]);

                                    // NOTE: Some keys have names such as "I Feel" (with a space in the string.
                                    // Need better way of fetching the rest of the command line as a single string.
                                    targetKeyName = fields[2];

                                    // Increment count of targets read from set thus far
                                    targetsReadFromSet++;
                                    // Advance index to next command in the script
                                    currentLine++;

                                    modeState = CalibrationStates.Create_Grouping;
                                }

                                else if (fields[0] == "SELECT")
                                {
                                    // Issue a command to a Empower (assumes displayed keyboard has already been fetched) 
                                    // in order to invoke the button's click pattern to perform things like clear the text box,
                                    // Go Back, or select typing keyboard.
                                    targetkeyIDvalue = Int32.Parse(fields[1]);
                                    targetKeyName = fields[2];

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
                                        ActivateUI();
                                    }

                                    // Give other app time to respond to command
                                    await Task.Delay(UI_delay_msec);

                                    // Advance index to next command in the script
                                    currentLine++;

                                    // After selecting a key on the display, restart the processing of fetching new set of
                                    // elements and creating a new overlay because key locations may have changed.
                                    modeState = CalibrationStates.Remote_Display;
                                }

                                else if (fields[0] == "STOP")
                                {
                                    // Advance index to next command in the script
                                    currentLine++;
                                    modeState = CalibrationStates.Stop_Message;
                                }

                                else
                                {
                                    Logger.Error(this, "ERROR: Unrecognized calibration command found in script");
                                    modeState = CalibrationStates.Clean_Up;
                                }
                            }
                            break;

                        case CalibrationStates.Remote_Display:
                            // Do what is necessary so that current displayed BCI app page is a transparent window
                            // in order to view displayed screen of other application (e.g. Empower).

                            modeState = CalibrationStates.Get_Remote_Elements;
                            break;

                        case CalibrationStates.Get_Remote_Elements:
                            // Call Microsoft UIA to fetch list of GUI elements seen on display generated by another app.
                            // Create list of generic elements by extracting only those properties necessary for flashing.
                            //RemoteDisplay remoteDisplay = new RemoteDisplay();

                            elementList = remoteDisplay.GetRemoteDisplayedElements();

                            if (elementList.Count == 0)
                            {
                                // Sometimes MUIA does not find the children elements of the desktop (root) element on the first try.
                                if (numGetElementRetries  < 2)
                                {
                                    await Task.Delay(200);
                                    // Try fetching the list one more time
                                    numGetElementRetries++;
                                    modeState = CalibrationStates.Get_Remote_Elements;
                                }
                                else
                                {
                                    logString = "Failed to fetch displayed elements using Microsoft UAI ... \n";
                                    Logger.Info(this, logString);
                                    modeState = CalibrationStates.Clean_Up;
                                }
                            }
                            else
                            {
                                // Extract boundary rectangle information of remote display.
                                DisplayProperties displayProperties = remoteDisplay.GetDisplayProperties();

                                // Create transparent screen overlay that is at same location and size as remote display.
                                stimulusOverlay = flashStimulus.CreateScreenOverlay(elementList, displayProperties);
                                if (notifyScreenOverLayChange != null) notifyScreenOverLayChange(stimulusOverlay);
                                // For each element, create a button that is the same size and location as seen on the display
                                // and add it to the screen overlay. Instead of sending MUIA commands to each individual element
                                // to flash them ON/OFF, make the visual changes to the buttons in the overlay and issue a
                                // single command to update the appearance of all the buttons at the same time.

                                /////////////////////////////////update UI with ScreenOverlay
                               

                                //Overlay newWindow = new Overlay();
                                //newWindow.Top = displayProperties.top;
                                //newWindow.Left = displayProperties.left;
                                //newWindow.Height = stimulusOverlay.Height;
                                //newWindow.Width = stimulusOverlay.Width;
                                //newWindow.WindowStyle = WindowStyle.None;
                                //stimulusOverlay.HorizontalAlignment = HorizontalAlignment.Stretch;
                                //stimulusOverlay.VerticalAlignment = VerticalAlignment.Stretch;
                                //newWindow.rootCanvas.Children.Add(stimulusOverlay);
                                //newWindow.Owner = this;
                                //newWindow.Show();

                                // Ready to map and assign keys from AAC display onto virtual keyboard overlay
                                modeState = CalibrationStates.Assign_Key_Mapping;
                            }
                            break;

                        case CalibrationStates.Local_Display:
                            // Have BCI app generate calibration display using URI in the DISPLAY command.
                            // This method allows creating default keyboards used in calibration and validation
                            // when using the BCI with non PRC applications.
                            // TO BE DONE: This functionality will be added after MVP

                            modeState = CalibrationStates.Read_Command;
                            break;

                        case CalibrationStates.Get_Local_Elements:
                            // Call function to fetch list of GUI elements seen on display generated by BCI app.
                            // TO BE DONE: This functionality will be added after MVP

                            // Ready to fetch next target from calibration commands
                            modeState = CalibrationStates.Assign_Key_Mapping;
                            break;

                        case CalibrationStates.Assign_Key_Mapping:
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

                            // Now that we know keyID mapping, can now preload stimulus images to specific overlay template
                            flashStimulus.AssignFlashImagesToButtons(flashableKeys, ref stimulusOverlay);

                            if (notifyScreenOverLayChange != null)
                            {
                                notifyScreenOverLayChange(stimulusOverlay);
                            }

                            modeState = CalibrationStates.Read_Command;
                            break;

                        case CalibrationStates.Create_Grouping:
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
                                if (randomMapping == true)
                                {
                                    randomValue = randomGen.Next(5);
                                }
                                else
                                {
                                    randomValue = 0;
                                }
                                groupingMatrix = flashGroups.GenerateGroupingMaxtrix(inputParam, keyIDMap, flashableKeys, gridData, randomValue, out matrixSize1, out matrixSize2);
                            }

                            modeState = CalibrationStates.Create_Sequence;
                            break;

                        case CalibrationStates.Create_Sequence:
                            // Convert rows and columns of hidden matrices into flash groups.
                            // Create flash sequence table of flash groupings.
                            FlashSequence flashSequence = new FlashSequence();

                            flashSequenceTable = flashSequence.GenerateFlashSequenceTable(groupingMatrix, matrixSize1, matrixSize2, targetkeyIDvalue);

                            if (testScriptEnabled == true)
                            {
                                // Show overlay first before highlight target next
                                flashStimulus.ShowScreenOverlayON(flashableKeys);
                            }

                            modeState = CalibrationStates.Highlight_Target;
                            break;

                        case CalibrationStates.Highlight_Target:

                            // Verify keyID value of current target matches one of the mapped keys
                            bool found = false;
                            for (int i = 0; i < flashableKeys.Count; i++)
                            {
                                if (flashableKeys[i].keyID == targetkeyIDvalue)
                                {
                                    found = true;
                                    break;
                                }
                            }

                            if (found == true)
                            {
                                flashStimulus.HighlightTargetON(targetkeyIDvalue);

                                if (targetsReadFromSet == 1 && showCountDownTimer)
                                {
                                    // Show count down timer for first target in the set
                                    showCountDownTimer = false;
                                    modeState = CalibrationStates.Wait_On_UI;
                                    NotifyUI(new UIInstruction { Type = UIType.CountDownWindow });
                                    break;
                                }

                                if (testScriptEnabled == true)
                                {
                                    modeState = CalibrationStates.Test_Script_Enabled;
                                }
                                else
                                {
                                    // Give user time to locate highlighted target when not using count down timer.
                                    await Task.Delay(timeToFindTarget);

                                    // Proceed on to normal flash stimulus
                                    modeState = CalibrationStates.Core_Loop;
                                }
                            }
                            else
                            {
                                MessagingService.SendMessage(this, "Target keyID does not match any active displayed keys", MessageType.Popup);
                                modeState = CalibrationStates.Clean_Up;
                            }

                            break;

                        case CalibrationStates.Test_Script_Enabled:
                            // Skip flashing keys and proceed to next step
                            MessagingService.SendMessage(this, "Test Script enabled, skip flashing stimulus step... ", MessageType.Popup);

                            flashStimulus.ShowScreenOverlayOFF(flashableKeys);

                            modeState = CalibrationStates.Unhighlight_Target;
                            break;


                        /* ********************************* */
                        /*  Real-time Processing Core Loop   */
                        /* ********************************* */

                        case CalibrationStates.Core_Loop:

                            bool displayStatus = true;
                            bool exitFlag = false;

                            int flashCount = 0;
                            int numIterations = 0;
                            int numEntries = flashSequenceTable.Count;


                            /* ********************************* */

                            if (calibrationFileMetadata ==null)
                            {
                                Logger.Error(this, "calibration file meta data is not available.unable to proceed");
                                modeState = CalibrationStates.Clean_Up; break;
                            }    
                           
                            binaryWriter = FileHandlerService.CreateAndOpenBinaryFile(calibrationFileMetadata.GetCurrentBinFileName(), calibrationFileMetadata.FilePath);
                            if (binaryWriter == null)
                            {
                                Logger.Error(this, "unable to create binary writer.");
                                modeState = CalibrationStates.Clean_Up; break;
                            }

                            /* ********************************* */


                            CancellationTokenSource pipelineTokenSource = new CancellationTokenSource();

                            DataflowBlockOptions blockOptions = new DataflowBlockOptions();
                            blockOptions.CancellationToken = pipelineTokenSource.Token;

                            // Create instances of BufferBlocks to pass data between pipelined tasks.
                            var flashDataBufferBlock = new BufferBlock<double[]>(blockOptions);
                            var flashMsgBufferBlock = new BufferBlock<int[]>(blockOptions);
                            var filterDataBufferBlock = new BufferBlock<double[]>(blockOptions);

                            int[] flashControlMessage = new int[MAX_FLASH_MSG_LENGTH];

                            // Create instances of classes that comprise the processing pipeline tasks
                            FlashTiming flashTiming = new FlashTiming();
                            SensorFiltering sensorFiltering = new SensorFiltering();
                            RecordBinaryData recordBinary = new RecordBinaryData();

                            // Start separate tasks that fetch sensor samples from headset while flashing keys on display screen,
                            // filtering the data for each sensor channel, and recording the filtered sensor data.

                            // Task t0 = Task.Run(() => headsetControl.ReadHeadsetDataAsync(headset, headsetTokenSource.Token, headsetDataBufferBlock, headsetMsgBufferBlock));

                            Task t1 = Task.Run(() => flashTiming.FlashDataAsync(headset, pipelineTokenSource.Token, targetkeyIDvalue, numFlashSeqPerTarget,
                                                                                flashSequenceTable, headsetDataBufferBlock, flashDataBufferBlock, flashMsgBufferBlock));

                            Task t2 = Task.Run(() => sensorFiltering.FilterDataAsync(pipelineTokenSource.Token, flashDataBufferBlock, filterDataBufferBlock));

                            Task t3 = Task.Run(() => recordBinary.RecordDataAsync(pipelineTokenSource.Token, filterDataBufferBlock, binaryWriter));


                            // Synchronize sensor dataflow of buffered data with flash sequence.
                            // headset.FlushBuffers();

                            // Enable the flow of sensor data from the headset to the processing pipeline now.
                            headsetControlMessage.content[0] = ENABLE_PIPELINE_SENSOR_DATA;     // message ID
                            headsetControlMessage.content[1] = 2;                               //message length

                            // Send command message to the headset task.
                            headsetMsgBufferBlock.Post(headsetControlMessage);

                            //await Task.Delay(100);

                            /* +++++++++++++++++++++++++++++++++++++++ */

                            while (exitFlag == false)
                            {
                                flashControlMessage = await flashTiming.GetFlashControlMessageAsync(flashMsgBufferBlock);

                                if(_forceExit)
                                {
                                    _forceExit = false;
                                    exitFlag=true;
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
                                    //Debug.WriteLine("{0}  Flash ON,  seq num = {1},  phase = {2} \n", flashTime, indexValue, phaseInSequence);

                                    if (displayStatus == true)
                                    {
                                        //Console.WriteLine("{0}  Start flashing for Target {1} \n", flashTime, numTargets);
                                        displayStatus = false;
                                    }
                                }
                                else if (messageType == FLASH_OFF)
                                {
                                    indexValue = flashControlMessage[1] - 1;
                                    phaseInSequence = flashControlMessage[2];
                                    elapsedTime = flashControlMessage[3];

                                    flashStimulus.FlashStimulusOFF(targetkeyIDvalue, indexValue, flashSequenceTable);

                                    sourceTimer = elapsedTime % 65536;
                                    //Console.WriteLine("{0}  Flash OFF,  seq num = {1},  phase = {2},   time = {3}  \n", flashTime, indexValue, phaseInSequence, sourceTimer);
                                    //Debug.WriteLine("{0}  Flash OFF,  seq num = {1},  phase = {2} \n", flashTime, indexValue, phaseInSequence);

                                    if (++flashCount >= numEntries)
                                    {
                                        flashCount = 0;
                                        ++numIterations;
                                        //Console.WriteLine("{0}  ... End flash iteration {1} \n", flashTime, numIterations);
                                    }
                                }
                                else if (messageType == END_SEQUENCE)
                                {
                                    //Console.WriteLine("{0}  ... End flashing for Target {1} \n", flashTime, numTargets);
                                    exitFlag = true;
                                }
                                else
                                {
                                    Logger.Info(this, flashTime + "FlashControlAsync() returned invalid message!!!  messageType = "+ messageType);
                                    exitFlag = true;
                                }

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

                            modeState = CalibrationStates.Unhighlight_Target;
                            break;

                        /* ********************************* */
                        /*  End Core Loop                    */
                        /* ********************************* */


                        case CalibrationStates.Unhighlight_Target:
                            // Undo changes and make target key transparent again
                            flashStimulus.HighlightTargetOFF(targetkeyIDvalue);

                            modeState = CalibrationStates.Click_Key;
                            break;

                        case CalibrationStates.Click_Key:
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
                                //let the ui know to come to focus again
                                ActivateUI();
                            }

                            // if (displayType == REMOTE)
                            //    - Call Microsoft UIA function to click key and let Empower do the work
                            // else
                            //    - Call function to show key name on local display

                            modeState = CalibrationStates.Programmed_Delay;
                            break;

                        case CalibrationStates.Programmed_Delay:
                            // Wait for programmable delay so Empower can complete its click repsonse.
                            // Need to wait for Empower to generate speech, display key name in text box,
                            // and possibly update keyboard display with new set of keys.
                            await Task.Delay(timeBetweenTargets);

                            // After selecting a key on the display, restart the processing of fetching new set of
                            // elements and creating a new overlay because key locations may have changed.
                            modeState = CalibrationStates.Remote_Display;
                            break;

                        case CalibrationStates.Clear_Textbox:
                            modeState = CalibrationStates.Stop_Message;
                            break;

                        case CalibrationStates.Stop_Message:

                            //Send UI message to show Stop query
                            NotifyUI(new UIInstruction { Type = UIType.CalibrationStopQuery });
                            modeState = CalibrationStates.Wait_On_UI;
                            break;


                        case CalibrationStates.Ready_To_Begin:
                            modeState = CalibrationStates.Wait_On_UI;
                            NotifyUI(new UIInstruction { Type = UIType.CalibrationBeginQuery });
                            break;

                        case CalibrationStates.Clean_Up:
                            // Close all open files and delete class objects (except headset)
                            modeState = CalibrationStates.Exit_Mode;
                            break;

                        case CalibrationStates.Exit_Mode:
                            // Now ready to return to the BCI app Main Menu
                            modeExit = true;
                            if (exitAction != null) exitAction();
                            break;

                    }  // switch (modeState)

                }  // while (!modeExit)

                // Stop headset task by cancelling the token
                headsetTokenSource.Cancel();
                headset.StopBackgroundAcquisition();
                await Task.Delay(50);

                headsetMsgBufferBlock.Complete();
                headsetDataBufferBlock.Complete();

                headset.Disconnect();
                headset.Delete();

                if (binaryWriter != null)
                {
                    FileHandlerService.CloseBinaryWriter(binaryWriter);
                }
                //if(calibrationCompleted&&completeAction!=null)
                //{
                  //  completeAction();
                //}
                //return Task.CompletedTask;
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
            if(modeState== CalibrationStates.Wait_On_UI)
            {
                uiInstruction = instruction;
                modeState = CalibrationStates.Process_UI_Notification;
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

        private void ActivateUI()
        {
            if(activeUIAction!=null) activeUIAction();
        }

        public void SetCalibrationFile(CalibrationFileMetadata calibrationFileMetadata)
        {
            this.calibrationFileMetadata = calibrationFileMetadata;
        }

        public void ForceExit()
        {
            _forceExit = true; 
        }
        /* ++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++ */
        /* ++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++ */
    }
}
