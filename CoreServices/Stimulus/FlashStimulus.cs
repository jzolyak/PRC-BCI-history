using CoreServices.Common;
using CoreServices.Presets;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;




namespace CoreServices.Stimulus
{
    public class FlashStimulus
    {
        // NOTE FOR MVP PRODUCT:
        //    The following constants are temporary "fudge factors" to get the screen overlay to line
        //    up on top of Empower display. This needs to be investigated and passed in as input parameters
        //    to CreateScreenOverlay(). Likewise, need different values for Empower demo versus Empower
        //    running on an Accent device.
        public const double SCALING_FACTOR = 1;
        public const int LEFT_OFFSET = 0;
        public const int TOP_OFFSET = 0;

        private FlashStimulusPresets flashStimulusPresets;
        private CalibrationPresets calibrationPresets;
        private bool isflashModeIsImage;
        private static readonly Dictionary<int, string> _imageNameMap = new();
        private string _buttonsImageFileNam;


        public FlashStimulus()
        {
            flashStimulusPresets = FlashStimulusPresets.Instance;
            calibrationPresets = CalibrationPresets.Instance;

            _buttonsImageFileNam = System.IO.Path.GetDirectoryName(Assembly.GetEntryAssembly().Location) + "\\Settings\\buttonImagesNmae.txt";
            LoadButtonIamgesFile(_buttonsImageFileNam);

            // Dynamically determine flash mode based on presets
            SetFlashModeFromPresets();

            // Subscribe to dynamic preset updates
            flashStimulusPresets.PresetsUpdated += OnPresetsUpdated;
        }

        List<ButtonElement> buttonList = new List<ButtonElement>();
        int[] crossReferenceTable = null;


        /* ++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++ */

        // public Canvas CreateScreenOverlay(List<ElementProperties> elementList, DisplayProperties displayProperties, out List<Button> listOfButtons)

        public ScreenOverlay CreateScreenOverlay(List<ElementProperties> elementList, DisplayProperties displayProperties)
        {
            int numElements = elementList.Count;



            buttonList = new List<ButtonElement>();
            var screenOverlay = new ScreenOverlay();
            // Use the bounding rectangle of the remote display to set the size of screen overlay
            screenOverlay.Height = displayProperties.bottom - displayProperties.top;
            screenOverlay.Width = displayProperties.right - displayProperties.left;

            screenOverlay.Top = displayProperties.top;
            screenOverlay.Left = displayProperties.left;
            var topOffset = displayProperties.top;
            var leftOffset = displayProperties.left;
            //Canvas.SetTop(screenOverlay, TOP_OFFSET);
            //Canvas.SetLeft(screenOverlay, LEFT_OFFSET);

            // Next, for every clickable element in the list create a corresponding button that is the
            // same size and same location as the original element on the display, and place it on the
            // screen overlay. There are several important reasons for doing it this way:
            //   1.  All the original GUI elements, regardless of type, can be flashed or highlighted
            //       using the same method applied to the button's properties on the screen overlay.
            //   2.  For the MVP product, the button background and border toggle from transparent to a
            //       set fixed color (Flash ON), and then back to transparent color (Flash OFF). In the
            //       future, researchers have requested option of using pictures of objects or faces
            //       overlayed on specifc keys to provide a visual stimulus. This method helps small
            //       children focus their attention longer, thus leading to a better evoked response.
            //   3.  Can flash all buttons simultaneously rather than sending individual commands to
            //       each original GUI element via MUIA to Flash ON and Flash OFF.


            // Create a 150 x 150 pixel black color square to serve as default.png image.
            // If see this actually on the screen, then no imaged assigned to keyID.
            string imageName = "default.png";

            for (int i = 0; i < numElements; i++)
            {
                var button = new ButtonElement()
                {

                    Width = SCALING_FACTOR * (elementList[i].right - elementList[i].left),
                    Height = SCALING_FACTOR * (elementList[i].bottom - elementList[i].top),

                    Top = (SCALING_FACTOR * (elementList[i].top - topOffset)),
                    Left = (SCALING_FACTOR * (elementList[i].left - leftOffset)),
                    FileName = imageName

                };
                buttonList.Add(button);

                screenOverlay.ButtonElementList.Add(button);
            }

            return screenOverlay;
        }

        /* ++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++ */

        public void CreateCrossReferenceTable(List<KeyMapping> flashableKeys)
        {
            int keyIDvalue;
            int numEntries = flashableKeys.Count;

            // Largest keyID value is located at end of the list
            int maxkeyIDvalue = flashableKeys[numEntries - 1].keyID;

            // There are generally more locations on keyboard grid than actual number of selectable keys.
            // Create direct lookup table between keyID value and button index instead of searching for match.
            crossReferenceTable = new int[maxkeyIDvalue + 1];

            for (int i = 0; i < maxkeyIDvalue; i++)
            {
                crossReferenceTable[i] = 0;
            }

            for (int i = 0; i < numEntries; i++)
            {
                keyIDvalue = flashableKeys[i].keyID;
                crossReferenceTable[keyIDvalue] = flashableKeys[i].buttonIndex;
            }
        }

        /* ++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++ */

        public void AssignFlashImagesToButtons(List<KeyMapping> flashableKeys, ref ScreenOverlay screenOverlay)
        {
            int keyIDvalue;
            int numEntries = flashableKeys.Count;

            for (int i = 0; i < numEntries; i++)
            {
                // NOTE: flashableKeys[] is a master list that ties the indexes of the MUIA element list, 
                //       button list, and keyID mapping together in one storage location. The number of
                //       nonblank (i.e. flashable) buttons is between 1 and the maximum keyID value.
                keyIDvalue = flashableKeys[i].keyID;

                // The user first creates a 4 x 4 matrix template of images that they want to use as a flash
                // stimulus pattern. Currently we don't have a software tool that takes the template and maps
                // the images to the keyID values assigned to the keyboard. Instead, the user creates a one
                // to one mapping between every keyID on the maximum size keyboard with the name of the PNG
                // picture file that is associated with each key.
                string imageName = GetImageNameByNumber(keyIDvalue);

                buttonList[i].FileName = imageName;

                screenOverlay.ButtonElementList[i].FileName = imageName;
            }
        }

        /* ++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++ */

        public void ShowScreenOverlayON(List<KeyMapping> flashableKeys)
        {
            int buttonIndex;

            int numEntries = flashableKeys.Count;

            // Since we are displaying all the flashable keys, don't need cross reference for buttonIndex
            for (int i = 0; i < numEntries; i++)
            {
                buttonIndex = flashableKeys[i].buttonIndex;

                buttonList[buttonIndex].BackgroundBrush = CustomBrushes.CornflowerBlue;
                buttonList[buttonIndex].BorderBrush = CustomBrushes.Yellow;
                buttonList[buttonIndex].Content = flashableKeys[i].keyID.ToString();
            }
        }

        /* ++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++ */

        public void ShowScreenOverlayOFF(List<KeyMapping> flashableKeys)
        {
            int buttonIndex;

            int numEntries = flashableKeys.Count;

            // Since we are displaying all the flashable keys, don't need cross reference for buttonIndex
            for (int i = 0; i < numEntries; i++)
            {
                buttonIndex = flashableKeys[i].buttonIndex;

                buttonList[buttonIndex].BackgroundBrush = CustomBrushes.Transparent;
                buttonList[buttonIndex].BorderBrush = CustomBrushes.Transparent;
                buttonList[buttonIndex].Content = "";
            }
        }



        /* ++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++ */

        public void HighlightTargetON(int keyID)
        {
            int buttonIndex;

            buttonIndex = crossReferenceTable[keyID];
            //buttonList[buttonIndex].BackgroundBrush = CustomBrushes.CornflowerBlue;
            //buttonList[buttonIndex].Opacity = 0.7;
            buttonList[buttonIndex].BorderBrush = calibrationPresets.TargetAppearancePresets.HighLightON_BorderBrush;
        }

        /* ++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++ */

        public void HighlightTargetOFF(int keyID)
        {
            int buttonIndex;

            buttonIndex = crossReferenceTable[keyID];
            //buttonList[buttonIndex].BackgroundBrush = CustomBrushes.Transparent;
            buttonList[buttonIndex].BorderBrush = CustomBrushes.Transparent;
        }

        /* ++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++ */

        public void FlashStimulusON(int targetKeyID, int entry, List<FlashTableEntry> flashSeqTable)
        {
            int keyID;
            int buttonIndex;

            for (int i = 0; i < FlashSequence.MAX_KEYS_PER_FLASH; i++)
            {
                keyID = flashSeqTable[entry].keyIDgrouping[i];

                if (keyID != 0)
                {
                    buttonIndex = crossReferenceTable[keyID];

                    if (isflashModeIsImage)
                    {
                        //buttonList[buttonIndex].FileName = GetImageNameByNumber(buttonIndex) ?? "default.png";
                        buttonList[buttonIndex].IsPicVisible = true;
                    }
                    else
                    {
                        buttonList[buttonIndex].BackgroundBrush = CustomBrushes.Brown;
                    }

                    

                    if (keyID == targetKeyID)
                    {   // Keep the target key highlighted
                        buttonList[buttonIndex].BorderBrush = CustomBrushes.Yellow;
                        //buttonList[buttonIndex].BorderBrush = CustomBrushes.Red;
                        //buttonList[buttonIndex].Opacity = 1.0;
                        //buttonList[buttonIndex].BackgroundBrush = CustomBrushes.CornflowerBlue;
                    }
                    else
                    {
                        buttonList[buttonIndex].BorderBrush = CustomBrushes.Yellow;
                    }
                }
                else
                {   // Reached end of key grouping
                    break;
                }
            }
        }

        /* ++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++ */

        public void FlashStimulusOFF(int targetKeyID, int entry, List<FlashTableEntry> flashSeqTable)
        {
            int keyID;
            int buttonIndex;

            for (int i = 0; i < FlashSequence.MAX_KEYS_PER_FLASH; i++)
            {
                keyID = flashSeqTable[entry].keyIDgrouping[i];

                if (keyID != 0)
                {
                    buttonIndex = crossReferenceTable[keyID];

                    
                    if (isflashModeIsImage)
                    {
                        //buttonList[buttonIndex].FileName = GetImageNameByNumber(buttonIndex) ?? "default.png";
                        buttonList[buttonIndex].IsPicVisible = false;
                        //hide the image
                    }
                    else
                    {
                        buttonList[buttonIndex].BackgroundBrush = CustomBrushes.Transparent;
                    }

                    if (keyID == targetKeyID)
                    {   // Keep the target key highlighted
                        buttonList[buttonIndex].BorderBrush = calibrationPresets.TargetAppearancePresets.HighLightON_BorderBrush;
                        //buttonList[buttonIndex].Opacity = 0.7;
                        //buttonList[buttonIndex].BackgroundBrush = CustomBrushes.CornflowerBlue;
                    }
                    else
                    {
                        buttonList[buttonIndex].BorderBrush = CustomBrushes.Transparent;
                    }
                }
                else
                {   // Reached end of key grouping
                    break;
                }
            }
        }

        /* ++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++ */

        private static string GetImageNameByNumber(int number)
        {
            return _imageNameMap.TryGetValue(number, out var name) ? name : "default.png";
        }

        private void LoadButtonIamgesFile(string filename)
        {
            try
            {
                if (!File.Exists(filename))
                {
                    Logger.Info(this, "buttonImagesName File does not exist");
                    return;
                }
                
                    foreach (var line in File.ReadAllLines(filename))
                    {
                        if (string.IsNullOrWhiteSpace(line)) continue;

                        int splitIndex = line.IndexOf(' ');
                        if (splitIndex <= 0) continue;

                        string numberPart = line.Substring(0, splitIndex).Trim();
                        string namePart = line.Substring(splitIndex + 1).Trim();

                        if (int.TryParse(numberPart, out int key) && !string.IsNullOrEmpty(namePart))
                        {
                            _imageNameMap[key] = namePart;
                        }
                    }
                
            }
            catch (Exception ex)
            {
                Logger.Error(this, ex.Message);
            }
        }

        private void SetFlashModeFromPresets()
        {
            var flashType = flashStimulusPresets.FlashAppearancePresets.flashType;

            switch (flashType)
            {
                case FlashAppearancePresets.FlashType.MonoColored:
                case FlashAppearancePresets.FlashType.ColoredPattern:
                    isflashModeIsImage = false;
                    Logger.Info(this, $"Flash mode set to COLOR ({flashType}).");
                    break;

                case FlashAppearancePresets.FlashType.PicturePattern:
                    isflashModeIsImage = true;
                    Logger.Info(this, "Flash mode set to IMAGE.");
                    break;

                default:
                    isflashModeIsImage = false;
                    Logger.Error(this, "Unknown flashType detected. Defaulting to COLOR mode.");
                    break;
            }
        }
        private void OnPresetsUpdated()
        {
            // Reload flash mode dynamically
            SetFlashModeFromPresets();

            // Optional: reload other appearance/timing parameters if needed
            // Example:
            // RefreshButtonColors();
            Logger.Info(this, "FlashStimulus updated flash mode from new presets.");
        }

    }

}