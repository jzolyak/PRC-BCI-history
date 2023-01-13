using BCI_Headset;
using CoreServices.Common;
using CoreServices.Files;
using CoreServices.Models;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Threading.Tasks.Dataflow;

namespace CoreServices.Calibration
{
    public class StateEngine
    {
        private RemoteDisplay remoteDisplay = new RemoteDisplay();
        private FlashStimulus flashStimulus = new FlashStimulus();
        private CalibrationStates modeState;
        private UIInstruction uiInstruction;
        private Action<UIInstruction> notifyUIAction;
        private Action<ScreenOverlay> notifyScreenOverLayChange;
        private CalibrationFileMetadata calibrationFileMetadata;


        private Action exitAction;
        private Action completeAction;

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

        public StateEngine(Action<UIInstruction> notifyUIAction,Action<ScreenOverlay> notifyScreenOverLayChange, Action exitAction,Action completeAction)
        {
            this.notifyUIAction = notifyUIAction;
            this.notifyScreenOverLayChange = notifyScreenOverLayChange;
            this.exitAction = exitAction;
            this.completeAction = completeAction;
        }

        public async Task CalibrationMode()
        {
            // NOTE: The following inputs must be "passed into" the Calibration Mode:
            //
            //   int startSetNum            // Which set number to begin at in the script
            //   int flash_ON_time_msec
            //   int flash_OFF_time_msec
            //   int delayBetweenTargets_msec
            //

            GroupingParam inputParam;
            inputParam.method = Method.NewMethod;
            inputParam.keyOrder = KeyOrder.Switchback;
            inputParam.boardDivision = BoardDivision.Checkerboard;
            inputParam.arrangement = Arrangement.Magic;
            inputParam.indexMapping = IndexMapping.GridSize;
            bool calibrationCompleted = false;

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

            int maxFlashSequences = 3;
            int flash_ON_time_msec = 90;
            int flash_OFF_time_msec = 60;

            int programmable_delay_msec = 2000;
            //int programmable_delay_msec = 10;

            string targetKeyName;
            string[] commandLines = null;

            bool modeExit = false;
            bool foundMatch = false;

            bool testScriptEnabled = false;

            const int MAX_MSG_LENGTH = 8;

            const int FLASH_ON = 1;
            const int FLASH_OFF = 2;
            const int END_SEQUENCE = 3;

            //bool testScriptEnabled = true;
            const int samplesPerBatch = 9;              // 9 samples = 30 msec @ Fs=300 Hz
            const double bufferAheadSec = 0.100;        // 100 msec

            bool passTest;
            int numTargets = 1;

            int messageType;
            int indexValue;
            int phaseInSequence;
            int elapsedTime;
            int sourceTimer;

            int[] messageBuffer = new int[MAX_MSG_LENGTH];

            string currentTime;


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

            /*   *****************************
            **   ***  Instantiate classes  ***   
            **   *****************************
            */



            string currentDirectory = Directory.GetCurrentDirectory();
            string scriptFilename = "CalibrationCommands.txt";
            string filePath = System.IO.Path.Combine(currentDirectory, scriptFilename);
            WearableSensingVR300 headset = new WearableSensingVR300();

            Logger.Info(this,"\nEntered program... \n");

            headset.CreateNew();
            headset.SetMessageVerbosity(2);

            string port = headset.GetDefaultHeadsetComPort();
            passTest = headset.ConnectPort(port);

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
                // Continue performing calibration until either:
                //   - Script runs to completion, user exits at end of set, or error occurs
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
                                await Task.Delay(programmable_delay_msec);

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

                                    // Close raw data file for this calibration set

                                    if (startSetNum > totalSetNum)
                                    {
                                        // Reached last set in the script file. Inform user calibration is finished.
                                        //Handover the calib files to generate coef and move the bin files to completed folder
                                        uiInstruction.Type = UIType.CalibrationComplete;
                                        uiInstruction.Result = false;
                                        NotifyUI(uiInstruction);
                                        calibrationCompleted = true;
                                        modeState = CalibrationStates.Clean_Up;
                                    }
                                    else
                                    {
                                        // Ask user if they want to continue with next calibration set
                                        modeState = CalibrationStates.Ready_To_Begin;
                                    }
                                }
                                else
                                {
                                    FileHandlerService.DeleteBinaryFile(calibrationFileMetadata.GetCurrentBinFileName(), "CalibrationFiles");
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
                                    modeState = CalibrationStates.Clean_Up;
                                }
                            }
                            break;
                        case CalibrationStates.Read_Command:

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
                                    modeState = CalibrationStates.Get_Remote_Elements;
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
                                    }

                                    // Give other app time to respond to command
                                    //await Task.Delay(2000);

                                    // Advance index to next command in the script
                                    currentLine++;
                                    modeState = CalibrationStates.Read_Command;
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

                            modeState = CalibrationStates.Read_Command;
                            break;

                        case CalibrationStates.Get_Remote_Elements:
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

                                // Ready to fetch next target from calibration commands
                                // modeState = CalibrationStates.Click_Key;     // <== This is second time I've fixed same error. JAZ 12/2/22
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

                            logString = "Number of list elements = " + elementList.Count.ToString() + "\n";
                            Logger.Info(this, logString);

                            gridData = keyboardGrid.ExtractGridData(elementList);

                            List<List<int>> listOfRows = keyboardGrid.CreateListOfRows(elementList);
                            List<List<int>> listOfColumns = keyboardGrid.CreateListOfColumns(elementList);
                            gridData.maxKeyRows = listOfRows.Count;
                            gridData.maxKeyCols = listOfColumns.Count;

                            logString = "Number of columns = " + listOfColumns.Count.ToString() + "\n";
                            Logger.Info(this, logString);

                            bool PRC_mode = true;

                            // Create a KeyID map of all the keys based on the mapping method and grid size
                            keyIDMap = keyboardGrid.CreateKeyIDMap(gridData, PRC_mode);

                            logString = "CreateKeyIDMap() ... \n";
                            Logger.Info(this, logString);

                            //flashableKeys = keyboardGrid.AssignKeyMapping(elementList, gridData, keyIDMap, out crossReferenceLookupTable);
                            flashableKeys = keyboardGrid.AssignKeyMapping(elementList, gridData, keyIDMap);

                            logString = "AssignKeyMapping() ... \n";
                            Logger.Info(this, logString);

                            // Create cross reference lookup table between the keyID value and buttonIndex value used in flashing keys.
                            flashStimulus.CreateCrossReferenceTable(flashableKeys);

                            logString = "CreateCrossReferenceTable() ... \n";
                            Logger.Info(this, logString);

                            modeState = CalibrationStates.Create_Grouping;
                            break;

                        case CalibrationStates.Create_Grouping:
                            // Run algorithms to randomize the assignment of flashable keys into two hidden matrices.
                            FlashGrouping flashGroups = new FlashGrouping();

                            groupingMatrix = flashGroups.GenerateGroupingMaxtrix(inputParam, keyIDMap, flashableKeys, gridData, out matrixSize1, out matrixSize2);

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
                                    showCountDownTimer = false;
                                    modeState = CalibrationStates.Wait_On_UI;
                                    NotifyUI(new UIInstruction { Type = UIType.CountDownWindow });
                                    break;
                                    // Show count down timer for first target in the set
                                    //////////////////show the countdown timer    
                                }

                                if (testScriptEnabled == true)
                                {
                                    modeState = CalibrationStates.Test_Script_Enabled;
                                }
                                else
                                {
                                    // Give user time to locate highlighted target when not using count down timer.
                                    await Task.Delay(programmable_delay_msec);

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

                            //int maxTableEntries = flashSequenceTable.Count;

                            //for (int iteration = 0; iteration < maxFlashSequences; iteration++)
                            //{
                            //    for (int entry = 0; entry < maxTableEntries; entry++)
                            //    {
                            //        flashStimulus.FlashStimulusON(targetkeyIDvalue, entry, flashSequenceTable);

                            //        // Pause to see flash color change
                            //        await Task.Delay(flash_ON_time_msec);

                            //        flashStimulus.FlashStimulusOFF(targetkeyIDvalue, entry, flashSequenceTable);
                            //        await Task.Delay(flash_OFF_time_msec);
                            //    }
                            //}

                            //modeState = CalibrationStates.Unhighlight_Target;
                            //break;

                            bool displayStatus = true;
                            bool exitFlag = false;

                            int flashCount = 0;
                            int numIterations = 0;
                            int numEntries = flashSequenceTable.Count;


                            headset.StartBackgroundAcquisition();

                            CancellationTokenSource tokenSource = new CancellationTokenSource();

                            var messageBufferBlock = new BufferBlock<int[]>();
                            var dataBufferBlock = new BufferBlock<double[]>();
                            if(calibrationFileMetadata ==null)
                            {
                                Logger.Error(this, "calibration file meta data is not available.unable to proceed");
                                modeState = CalibrationStates.Clean_Up; break;
                            }    
                            binaryWriter = FileHandlerService.CreateAndOpenBinaryFile(calibrationFileMetadata.GetCurrentBinFileName(), "CalibrationFiles");
                            if (binaryWriter == null)
                            {
                                Logger.Error(this, "unable to create binary writer.");
                                modeState = CalibrationStates.Clean_Up; break;
                            }
                            // Start separate tasks that fetch sensor samples from headset while flashing keys
                            // on display screen and recording the raw data.
                            Task t1 = Task.Run(() => FlashControlAsync(headset, tokenSource.Token, flashSequenceTable, messageBufferBlock, dataBufferBlock));

                            Task t2 = Task.Run(() => RecordDataAsync(tokenSource.Token, dataBufferBlock, binaryWriter));

                            while (exitFlag == false)
                            {
                                messageBuffer = await GetFlashControlMessageAsync(messageBufferBlock);

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

                                    if (displayStatus == true)
                                    {
                                        //Console.WriteLine("{0}  Start flashing for Target {1} \n", flashTime, numTargets);
                                        displayStatus = false;
                                    }
                                }
                                else if (messageType == FLASH_OFF)
                                {
                                    indexValue = messageBuffer[1] - 1;
                                    phaseInSequence = messageBuffer[2];
                                    elapsedTime = messageBuffer[3];

                                    flashStimulus.FlashStimulusOFF(targetkeyIDvalue, indexValue, flashSequenceTable);

                                    sourceTimer = elapsedTime % 65536;
                                    //Console.WriteLine("{0}  Flash OFF,  seq num = {1},  phase = {2},   time = {3}  \n", flashTime, indexValue, phaseInSequence, sourceTimer);

                                    if (++flashCount >= numEntries)
                                    {
                                        flashCount = 0;
                                        ++numIterations;
                                        //Console.WriteLine("{0}  ... End flash iteration {1} \n", flashTime, numIterations);
                                    }
                                }
                                else if (messageType == END_SEQUENCE)
                                {
                                    indexValue = messageBuffer[1];
                                    //Console.WriteLine("{0}  ... End flashing for Target {1} \n", flashTime, numTargets);
                                    exitFlag = true;
                                }
                                else
                                {
                                    
                                    Logger.Info(this, flashTime + "OH !!!  messageType = "+ messageType);
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

                            modeState = CalibrationStates.Unhighlight_Target;
                            break;

                        /* ********************************* */
                        /*  End Core Loop                    */
                        /* ********************************* */

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
                            await Task.Delay(programmable_delay_msec);

                            // Fetch next target in calibration set
                            modeState = CalibrationStates.Read_Command;
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

                // this.Close();

                headset.Disconnect();
                headset.Delete();

                if (binaryWriter != null)
                {
                    FileHandlerService.CloseBinaryWriter(binaryWriter);
                }
                if(calibrationCompleted&&completeAction!=null)
                {
                    completeAction();
                }
                //return Task.CompletedTask;
            }
            catch (Exception ex)
            {

            }
        }

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

        public void SetCalibrationFile(CalibrationFileMetadata calibrationFileMetadata)
        {
            this.calibrationFileMetadata = calibrationFileMetadata;
        }


        public void FlashControlAsync(IHeadset headset, CancellationToken cts, List<FlashTableEntry> flashSequenceTable,
                                      ITargetBlock<int[]> messageBufferBlock, ITargetBlock<double[]> dataBufferBlock)
        {
            const int MAX_CHANNELS = 7;             // Assumes no TRIG channel in montage, just (sensor - ref) values
            const int CIRCULAR_BUF_LEN = 512;       // Num sensor samples per chan in the circular buffer

            const int MAX_MSG_LENGTH = 8;
            const int MARKER_LENGTH = 5;

            const int FLASH_ON = 1;
            const int FLASH_OFF = 2;
            const int END_SEQUENCE = 3;

            const int msecPerSecond = 1000;
            const int sampleRate = 300;             // 300 samples per second

            const int samplesPerBatch = 9;          // 9 samples = 30 msec @ Fs=300 Hz

            const double PRESET_1 = 1000.0;         // 1.0 second
            const double PRESET_2 = 1000.0;         // 1.0 second
            const double PRESET_3 = 90.0;           // 90 msec = Flash ON
            const double PRESET_4 = 60.0;           // 60 msec = Flash OFF
                                                    //            const double PRESET_5 = 800.0;          // 800 msec delay after last flash
            const double PRESET_5 = 800.0;          // 800 msec delay after last flash

            const double batchIntervalMsec = (double)((samplesPerBatch * msecPerSecond) / sampleRate);

            int channelIndex;
            int sampleIndex;

            int numEntries = flashSequenceTable.Count;

            int listIndex = 0;
            int flashCycleCount = 0;

            int batchCounter = 0;           // Track # of batches in 1.0 second
            int phaseInSequence = 0;
            int sequenceCounter = 0;

            int bufferIndex;
            int dataIndex;
            int circBufReadIndex = 0;
            int circBufWriteIndex = 0;

            int[] messageBuffer = new int[MAX_MSG_LENGTH];

            double t0;

            double timeElapsed;
            double signalValue;

            // Markers stored with every raw data sensor sample value
            double phInSeq = 0.0;
            double matrixPos = 0.0;
            double sequenceNum = 0.0;
            double target = 0.0;
            double stimulusON = 0.0;
            double dataValue = 0.0;


            double[] keyIDs = new double[FlashSequence.MAX_KEYS_PER_FLASH] { 0.0, 0.0, 0.0, 0.0, 0.0, 0.0, 0.0, 0.0, 0.0, 0.0 };

            double[,] CircularBuffer = new double[MAX_CHANNELS, CIRCULAR_BUF_LEN];

            double[] dataBuffer = new double[(MAX_CHANNELS + MARKER_LENGTH + FlashSequence.MAX_KEYS_PER_FLASH) * samplesPerBatch];

            // Stopwatch stopwatch = new Stopwatch();

            /* +++++++++++++++++++++++++++++++++++++++ */

            int numberOfChannels = headset.GetNumberOfChannels();

            while (!cts.IsCancellationRequested)
            {
                // This blocking call aims to ensure that batches of samples are picked
                // up at exactly regular intervals (t0 timestamps should be very regular)
                t0 = headset.WaitForBatch();

                // stopwatch.Start();

                batchCounter++;

                timeElapsed = batchCounter * batchIntervalMsec;

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

                        //dataValue += 1.0;
                        //dataValue = (double)((channelIndex * 10) + sampleIndex);
                        //CircularBuffer[channelIndex, bufferIndex] = dataValue;

                        bufferIndex = (++bufferIndex) & (CIRCULAR_BUF_LEN - 1);
                        //bufferIndex += 1;
                        //bufferIndex = (bufferIndex) & (CIRCULAR_BUF_LEN - 1);
                    }
                }

                circBufWriteIndex = (circBufWriteIndex + samplesPerBatch) & (CIRCULAR_BUF_LEN - 1);

                /* ++++++++++++++++++++++++++++++++++++++ */

                // NOTE:  Rather than pass a mix of floating point raw sensor data and integer marker values
                // between tasks and file storage, the marker and key index data are sent as doubles.

                switch (phaseInSequence)
                {
                    case 0:
                        // Assign default value to markers for Phase 0
                        if (batchCounter == 1)
                        {
                            phInSeq = (double)phaseInSequence;
                            matrixPos = 0.0;
                            sequenceNum = 0.0;
                            target = 0.0;
                            stimulusON = 0.0;

                            for (int i = 0; i < FlashSequence.MAX_KEYS_PER_FLASH; i++)
                            {
                                keyIDs[i] = 0.0;
                            }
                        }

                        if (timeElapsed >= PRESET_1)
                        {
                            phaseInSequence = 1;
                            batchCounter = 0;
                        }
                        break;

                    case 1:
                        // Assign default value to markers for Phase 1
                        if (batchCounter == 1)
                        {
                            phInSeq = (double)phaseInSequence;
                            matrixPos = 0.0;
                            sequenceNum = 0.0;
                            target = 0.0;
                            stimulusON = 0.0;

                            for (int i = 0; i < FlashSequence.MAX_KEYS_PER_FLASH; i++)
                            {
                                keyIDs[i] = 0.0;
                            }
                        }

                        if (timeElapsed >= PRESET_2)
                        {   // Advance to next phase with stimulus flashing
                            phaseInSequence = 2;
                            batchCounter = 0;
                        }
                        break;

                    case 2:
                        // Fetch marker values from Flash Sequence table
                        if (batchCounter == 1)
                        {
                            phInSeq = (double)phaseInSequence;
                            stimulusON = 1.0;

                            // Fetch marker value and convert them to double at start of each new flash
                            matrixPos = (double)flashSequenceTable[listIndex].matrixPosition;
                            sequenceNum = (double)flashSequenceTable[listIndex].sequenceNum;
                            target = (double)flashSequenceTable[listIndex].target;

                            for (int i = 0; i < FlashSequence.MAX_KEYS_PER_FLASH; i++)
                            {
                                keyIDs[i] = (double)flashSequenceTable[listIndex].keyIDgrouping[i];
                            }

                            messageBuffer[0] = FLASH_ON;
                            messageBuffer[1] = flashSequenceTable[listIndex].sequenceNum;
                            messageBuffer[2] = phaseInSequence;
                            // messageBuffer[3] = batchCounter;
                            // messageBuffer[3] = (int)(t0 * 10000.0);
                            messageBuffer[3] = (int)(t0 * 1000.0);
                            // messageBuffer[3] = (int)(timeElapsed * 100.0);

                            messageBufferBlock.Post(messageBuffer);
                        }

                        // NOTE: Value for Preset3 (Flash ON) must be >= Preset4 (Flash OFF)
                        // if ( (timeElapsed > PRESET_3) && (stimulusON == 1.0) )
                        if (stimulusON == 1.0)
                        {
                            if (timeElapsed > PRESET_3)
                            {
                                // batchCounter = 0;
                                stimulusON = 0.0;

                                messageBuffer[0] = FLASH_OFF;
                                messageBuffer[1] = flashSequenceTable[listIndex].sequenceNum;
                                messageBuffer[2] = phaseInSequence;
                                // messageBuffer[3] = batchCounter;
                                // messageBuffer[3] = (int)(t0 * 10000.0);
                                messageBuffer[3] = (int)(t0 * 1000.0);
                                // messageBuffer[3] = (int)(timeElapsed * 100.0);

                                messageBufferBlock.Post(messageBuffer);
                            }
                        }

                        else
                        {
                            if (timeElapsed >= (PRESET_3 + PRESET_4))
                            {
                                batchCounter = 0;

                                // Need to increment index into Stimulus table to read next entry
                                if (++listIndex == numEntries)
                                {
                                    listIndex = 0;

                                    // Need to keep count of entire # times passed thru entire flash sequence list
                                    //                                if (++flashCycleCount > 4)
                                    if (++flashCycleCount > 4)
                                    {
                                        phaseInSequence = 3;
                                        batchCounter = 0;
                                    }
                                }
                            }
                        }
                        break;

                    case 3:
                        // Assign default value to markers for Phase 3
                        if (batchCounter == 1)
                        {
                            phInSeq = (double)phaseInSequence;
                            matrixPos = 0.0;
                            sequenceNum = 0.0;
                            target = 0.0;
                            stimulusON = 0.0;

                            for (int i = 0; i < FlashSequence.MAX_KEYS_PER_FLASH; i++)
                            {
                                keyIDs[i] = 0.0;
                            }
                        }

                        if (timeElapsed >= PRESET_5)
                        {
                            phaseInSequence = 0;
                            batchCounter = 0;

                            listIndex = 0;
                            flashCycleCount = 0;
                            dataValue = 0.0;

                            messageBuffer[0] = END_SEQUENCE;
                            messageBuffer[1] = sequenceCounter;
                            messageBufferBlock.Post(messageBuffer);
                        }
                        break;
                }  // switch(phaseInSequence)

                /* ++++++++++++++++++++++++++++++++++++++ */

                // 
                dataIndex = 0;
                double bufferLevel = (double)headset.GetNumberOfBufferedSamples();

                bufferIndex = circBufReadIndex;
                for (sampleIndex = 0; sampleIndex < samplesPerBatch; sampleIndex++)
                {
                    for (channelIndex = 0; channelIndex < numberOfChannels; channelIndex++)
                    {
                        signalValue = CircularBuffer[channelIndex, bufferIndex];
                        dataBuffer[dataIndex++] = signalValue;

                        //dataValue += 1.0;
                        //dataBuffer[dataIndex++] = dataValue;
                    }

                    dataBuffer[dataIndex++] = phInSeq;

                    //dataBuffer[dataIndex++] = matrixPos;
                    //dataBuffer[dataIndex++] = t0;
                    dataBuffer[dataIndex++] = bufferLevel;

                    dataBuffer[dataIndex++] = sequenceNum;
                    dataBuffer[dataIndex++] = target;
                    dataBuffer[dataIndex++] = stimulusON;

                    for (int i = 0; i < FlashSequence.MAX_KEYS_PER_FLASH; i++)
                    {
                        dataBuffer[dataIndex++] = keyIDs[i];
                    }

                    bufferIndex = (++bufferIndex) & (CIRCULAR_BUF_LEN - 1);
                    //bufferIndex = (bufferIndex++) & (CIRCULAR_BUF_LEN - 1);
                }

                dataBufferBlock.Post(dataBuffer);

                circBufReadIndex = (circBufReadIndex + samplesPerBatch) & (CIRCULAR_BUF_LEN - 1);

                /* ++++++++++++++++++++++++++++++++++++++ */

                /*
                // stopwatch.Stop();
                // int stopwatchElasped = stopwatch.Elapsed.Milliseconds;
                */

                /* ++++++++++++++++++++++++++++++++++++++ */

            }  // while (!cts.IsCancellationRequested)

        }  // FlashControlAsync()

        /* ++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++ */
        /* ++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++ */

        public async Task RecordDataAsync(CancellationToken cts, ISourceBlock<double[]> dataBufferBlock, BinaryWriter bw)
        {
            int blockSize;

            while (!cts.IsCancellationRequested)
            {
                double[] rawDataBuffer = await dataBufferBlock.ReceiveAsync();


                // Store raw data into binary file
                try
                {
                    blockSize = rawDataBuffer.Length;

                    for (int i = 0; i < blockSize; i++)
                    {
                        bw.Write(rawDataBuffer[i]);
                    }
                }
                catch (IOException e)
                {
                    Logger.Error(this,e.Message + "\n Cannot record binary data to file.");
                    return;
                }

            }
        }

        /* ++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++ */
        /* ++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++ */

        public async Task<int[]> GetFlashControlMessageAsync(ISourceBlock<int[]> messageBufferBlock)
        {
            int[] messageBuffer = await messageBufferBlock.ReceiveAsync();

            return messageBuffer;
        }

        /* ++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++ */
        /* ++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++ */
    }
}
