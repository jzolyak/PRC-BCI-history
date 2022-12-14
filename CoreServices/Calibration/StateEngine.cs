using CoreServices.Common;
using CoreServices.Models;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

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

        public StateEngine(Action<UIInstruction> notifyUIAction,Action<ScreenOverlay> notifyScreenOverLayChange, Action exitAction)
        {
            this.notifyUIAction = notifyUIAction;
            this.notifyScreenOverLayChange = notifyScreenOverLayChange;
            this.exitAction = exitAction;
            
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
            //bool testScriptEnabled = true;


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
                Console.WriteLine(filePath + " does not exist!");
                modeState = CalibrationStates.Clean_Up;
            }
            */
            try
            {
                commandLines = File.ReadAllLines(filePath);
                maxLinesInFile = commandLines.Length;
                currentLine = 0;
                repeatCalibrationLine = 0;

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
                                        uiInstruction.Type = UIType.CalibrationComplete;
                                        uiInstruction.Result = false;
                                        NotifyUI(uiInstruction);
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
                                showCountDownTimer = false;
                                modeState = CalibrationStates.Highlight_Target;
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
                                            Console.WriteLine("ERROR: Can't find starting set number in calibration commands");
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
                                        Console.WriteLine("ERROR: Invalid DISPLAY calibration command");
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
                                    modeState = CalibrationStates.Assign_Key_Mapping;
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

                                    remoteDisplay.SelectElement(elementIndex);

                                    // Give other app time to respond to command
                                    // await Task.Delay(2000);

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
                                    Console.WriteLine("ERROR: Unrecognized calibration command found in script");
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

                            if (elementList == null)
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
                                    MessagingService.SendMessage(this, "Failed to fetch displayed elements using Microsoft UAI... ", MessageType.Popup);
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
                                modeState = CalibrationStates.Read_Command;
                            }
                            break;

                        case CalibrationStates.Local_Display:
                            // Have BCI app generate calibration display using URI in the DISPLAY command.
                            // This method allows creating default keyboards used in calibration and validation
                            // when using the BCI with non PRC applications.
                            // TO BE DONE: This functionality will be added after MVP

                            modeState = CalibrationStates.Get_Local_Elements;
                            break;

                        case CalibrationStates.Get_Local_Elements:
                            // Call function to fetch list of GUI elements seen on display generated by BCI app.
                            // TO BE DONE: This functionality will be added after MVP

                            // Ready to fetch next target from calibration commands
                            modeState = CalibrationStates.Read_Command;
                            break;

                        case CalibrationStates.Assign_Key_Mapping:
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

                            //flashableKeys = keyboardGrid.AssignKeyMapping(elementList, gridData, keyIDMap, out crossReferenceLookupTable);
                            flashableKeys = keyboardGrid.AssignKeyMapping(elementList, gridData, keyIDMap);

                            // Create cross reference lookup table between the keyID value and buttonIndex value used in flashing keys.
                            flashStimulus.CreateCrossReferenceTable(flashableKeys);

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

                            int maxTableEntries = flashSequenceTable.Count;

                            for (int iteration = 0; iteration < maxFlashSequences; iteration++)
                            {
                                for (int entry = 0; entry < maxTableEntries; entry++)
                                {
                                    flashStimulus.FlashStimulusON(targetkeyIDvalue, entry, flashSequenceTable);

                                    // Pause to see flash color change
                                    await Task.Delay(flash_ON_time_msec);

                                    flashStimulus.FlashStimulusOFF(targetkeyIDvalue, entry, flashSequenceTable);
                                    await Task.Delay(flash_OFF_time_msec);
                                }
                            }

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

                            remoteDisplay.SelectElement(elementIndex);

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
    }
}
