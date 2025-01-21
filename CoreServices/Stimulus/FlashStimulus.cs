using System;
using System.Collections.Generic;
using System.Linq;
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

            for (int i = 0; i < numElements; i++)
            {
                var button = new ButtonElement()
                {

                    Width = SCALING_FACTOR * (elementList[i].right - elementList[i].left),
                    Height = SCALING_FACTOR * (elementList[i].bottom - elementList[i].top),

                    Top = (SCALING_FACTOR * (elementList[i].top - topOffset)),
                    Left = (SCALING_FACTOR * (elementList[i].left - leftOffset))
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
            buttonList[buttonIndex].BackgroundBrush = CustomBrushes.CornflowerBlue;
            buttonList[buttonIndex].BorderBrush = CustomBrushes.Red;
        }

        /* ++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++ */

        public void HighlightTargetOFF(int keyID)
        {
            int buttonIndex;

            buttonIndex = crossReferenceTable[keyID];
            buttonList[buttonIndex].BackgroundBrush = CustomBrushes.Transparent;
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

                    buttonList[buttonIndex].BackgroundBrush = CustomBrushes.Brown;

                    if (keyID == targetKeyID)
                    {   // Keep the target key highlighted
                        buttonList[buttonIndex].BorderBrush = CustomBrushes.Red;
                        buttonList[buttonIndex].Opacity = 0.6;
                        buttonList[buttonIndex].BackgroundBrush = CustomBrushes.CornflowerBlue;
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

                    buttonList[buttonIndex].BackgroundBrush = CustomBrushes.Transparent;

                    if (keyID == targetKeyID)
                    {   // Keep the target key highlighted
                        buttonList[buttonIndex].BorderBrush = CustomBrushes.Red;
                        buttonList[buttonIndex].Opacity = 0.6;
                        buttonList[buttonIndex].BackgroundBrush = CustomBrushes.CornflowerBlue;
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

    }

    public enum CustomBrushes
    {
        Transparent,
        Red,
        Green,
        Blue,
        Brown,
        Yellow,
        CornflowerBlue
    }
}