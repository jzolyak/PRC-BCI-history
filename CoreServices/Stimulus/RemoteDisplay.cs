using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using CoreServices.Common;

using UIAutomationClient;

using System.Runtime.InteropServices;
using System.Diagnostics;

namespace CoreServices.Stimulus
{ 
    public class RemoteDisplay
    {

        [System.Runtime.InteropServices.DllImport("user32.dll")]
        private static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        static extern bool IsIconic(IntPtr hWnd);

        DisplayProperties displayProperties;

        IUIAutomationElementArray automationElementArray;
       public ConcurrentDictionary<int,IUIAutomationElement> automationElementList = new ConcurrentDictionary<int, IUIAutomationElement>();

        /* ++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++ */

        public DisplayProperties GetDisplayProperties()
        {
            return displayProperties;
        }

        /* ++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++ */

        public IUIAutomationElementArray GetAutomationElementArray()
        {
            // Use Empower as default remote app for minimum viable product.
            // In future, can work with other apps by passing parameters into this function.
            // int propertyID = 300005;  // UIA_NamePropertyId
            // string elementName = "Empower™";

            int left;
            int top;
            int right;
            int bottom;

            string logString;

            // Declare handle to an empty array. If MUIA fails to return an array of elements
            // then the length of automationArray[] will be zero.
            // IUIAutomationElementArray automationArray = new IUIAutomationElement[] { };
            // IUIAutomationElementArray automationArray = Array.Empty<IUIAutomationElement>();
            
            IUIAutomationElementArray automationArray;


            IUIAutomation automation = new CUIAutomation();
            IUIAutomationElement rootElement = automation.GetRootElement();

            //Process p=null;

            if (rootElement != null)
            {
                IUIAutomationCondition condition = automation.CreatePropertyCondition(30005, "Empower™");

                // Get the parent element of all the children elements of the remote application.
                IUIAutomationElement appElement = rootElement.FindFirst(TreeScope.TreeScope_Children, condition);

                /*
                if(appElement == null)
                {
                    p = GetEmpowerProcess();
                    if(p != null)
                    {
                        condition = automation.CreatePropertyCondition(30002,p.Id);
                        appElement = rootElement.FindFirst(TreeScope.TreeScope_Children, condition);
                    }
                }
                */

                if (appElement != null)
                {
                    //check to see if the window is in minimize mode;
                    //if(p==null)
                    //{
                    //    p = GetEmpowerProcess();
                    //    if (p != null && IsIconic(p.MainWindowHandle))
                    //    {
                    //        ShowWindow(p.MainWindowHandle, 3);
                    //    }

                    //}
                    // Extract properties of application display size
                    left = appElement.CurrentBoundingRectangle.left;
                    top = appElement.CurrentBoundingRectangle.top;
                    right = appElement.CurrentBoundingRectangle.right;
                    bottom = appElement.CurrentBoundingRectangle.bottom;

                    displayProperties = new DisplayProperties(left, top, right, bottom);

                    // Get array with all the children automation elements shown on the remote display.
                    automationArray = appElement.FindAll(TreeScope.TreeScope_Descendants, automation.CreateTrueCondition());

                    if (automationArray.Length == 0)
                    {   // Indicate to calling routine that there were no elements found.
                        automationArray = null;

                        // MessagingService.SendMessage(this,"Microsoft UIA can't find any children elements!",MessageType.Popup);
                        logString = "Microsoft UIA can't find any children elements ... \n";
                        Logger.Info(this, logString);
                    }
                }
                else
                {
                    automationArray = null;

                    // MessagingService.SendMessage(this, "Microsoft UIA can't find appElement!", MessageType.Popup);
                    logString = "Microsoft UIA can't find appElement ... \n";
                    Logger.Info(this, logString);
                }
            }
            else
            {
                automationArray = null;

                // MessagingService.SendMessage(this, "Microsoft UIA can't find rootElement!", MessageType.Popup);
                logString = "Microsoft UIA can't find rootElement ... \n";
                Logger.Info(this, logString);
            }

            return automationArray;
        }

        /* ++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++ */

        public List<ElementProperties> FilterAutomationElementArray(IUIAutomationElementArray automationArray)
        {
            // Use Empower as default remote app for minimum viable product.
            // In future, can work with other apps by passing parameters into this function.
            // enum filterRules = ?    // May need different set of selectable rules in which to chose
            //                            clickable elements for remote app.
            // enum elementTypes = ?   // List of element types defined as clickable (i.e. flashable).

            // NOTE:
            //    1)  For MVP, only concerned about finding "button" type on Empower display
            //    2)  Empower keyboard screen has 2 peculiar properties (i.e. "rules")
            //        (i)   The Empower keyboard has many blank keys (i.e. no name, label, or icon displayed on key surface).
            //              If the key is marked as Enabled in the MUIA, it is added to the clickable list, otherwise the
            //              blank key is not selectable, and therefore it is not flashed.
            //        (ii)  A key is constructed out of 2 nested GUI elements, i.e. it is a button inside another button. 
            //              Only the outer button is considered the clickable element if it is enabled.

            bool addToList;

            int left;
            int top;
            int right;
            int bottom;

            int index = 0;
            int peakIndex = 0;
            int numElements = automationArray.Length;

            List<ElementProperties> clickableElements = new List<ElementProperties>();

			automationElementList.Clear();							  
            while (index < numElements)
            {
                addToList = false;

                IUIAutomationElement b1 = automationArray.GetElement(index);

                // Consume the element from the MUIA list.
                index++;

                if (b1.CurrentLocalizedControlType == "button")
                {
                    // This search algorithm solely focuses on finding the outer boundary of each button
                    // that is located on the keyboard. It doesn't matter if the button is active or empty.
                    // If this routine detects a GUI element that is not a button, then it is not added to
                    // the returned list of selectable elements on the keyboard.

                    // Need to examine the next element in the MUIA list to interpret the current button element.
                    IUIAutomationElement b2 = automationArray.GetElement(index);

                    if (b2.CurrentLocalizedControlType == "button")
                    {
                        // Next we need to examine the border of the second button to determine if
                        //  - It is an inner button with/without a label, or
                        //  - It is the next button in the same row (to the right of the current button), or
                        //  - It is the next button in the next row (below the current row), or
                        //  - The second button has no dimensions, marking the outer button as an empty button (Dear god! Why?)

                        // Can't use " >= " for testing button inside button, it must be just ">" test,
                        // otherwise this case gets confused with another.

                        if ((b2.CurrentBoundingRectangle.top > b1.CurrentBoundingRectangle.top) &&
                            (b1.CurrentBoundingRectangle.bottom > b2.CurrentBoundingRectangle.bottom))
                        {
                            peakIndex = index + 1;

                            IUIAutomationElement b3 = automationArray.GetElement(peakIndex);

                            if (b3.CurrentLocalizedControlType == "text")
                            {
                                // Found instance of "Button -> Button -> Textbox"
                                // Add b1 button to list of selectable keys
                                addToList = true;

                                // Consume the b2 button and textbox from the MUIA element list.
                                index = index + 2;
                            }
                            else
                            {
                                // Found instance of "Button -> Button", but no label
                                // Add b1 button to list of selectable keys
                                addToList = true;

                                // Consume just the b2 button from the MUIA element list.
                                index = index + 1;
                            }
                        }

                        else if ((b1.CurrentBoundingRectangle.top == b2.CurrentBoundingRectangle.top) &&
                                 (b2.CurrentBoundingRectangle.right > b1.CurrentBoundingRectangle.right))
                        {
                            // Found instance of next button in same row (i.e. same top value)
                            // Add b1 button to list of selectable keys
                            addToList = true;

                            // Don't update MUIA list index. Re-examine b2 instead as next outer button b1.
                        }

                        else if ((b2.CurrentBoundingRectangle.top > b1.CurrentBoundingRectangle.top) &&
                                 (b2.CurrentBoundingRectangle.bottom > b1.CurrentBoundingRectangle.bottom))
                        {
                            // Found instance of next button in next row
                            // Add button to report of selectable keys
                            addToList = true;

                            // Don't update MUIA list index. Re-examine b2 instead as next outer button b1.
                        }

                        else if ((b2.CurrentBoundingRectangle.top == 0) &&
                                (b2.CurrentBoundingRectangle.bottom == 0))
                        {
                            // Found special case of outer button b1 being marked as Empty
                            // Add button to list of selectable keys
                            addToList = true;

                            // Update MUIA list index to consume "empty" button b2.
                            index = index + 1;
                        }

                        else if ((b2.CurrentBoundingRectangle.top == b1.CurrentBoundingRectangle.top) &&
                                (b2.CurrentBoundingRectangle.left == b1.CurrentBoundingRectangle.left))
                        {
                            // Full Screen mode and Icon Tutor button work around: 02-11-2025
                            // Found 2 additional special cases. In "Full Screen" mode, the message textbox goes away.
                            // The MENU button is now half the height, twice the width as normal key, and it is *ON TOP*
                            // of the regular key board. In MUAI, the Empower MENU is listed as two buttons with the exact
                            // same MUIA dimensions and button location.
                            //
                            // At some point around Empower V1.25, a change was made to the format of the icon tutor buttons
                            // that are underneath the message textbox on the typing keyboard. In MUAI, Empower reports each
                            // icon tutor buttons as two buttons with the exact same MUIA dimensions and button location. 
                            //
                            // The best fix for this problem is to mark the MENU button and icon tutor button's property
                            // "IsEnabled = 0" so that the BCI can ignore those keys and not make them flashable/selectable
                            // by the BCI app. (This is now being done for the Empty Appearance keys that have NO Action).
                            //
                            // As a work around solution we are going to ignore any two consecutive button elements in the
                            // MUIA list that have the exact same button dimension/location.

                            // DO NOT add button to list of selectable keys
                            addToList = false;

                            // Update MUIA list index to consume MENU key (buttons b1 & b2).
                            index = index + 1;

                        }  // if b2 is an inner button


                    }
                    else
                    {
                        // Discard the singleton button b1, as well as the element that followed it.
                        // Consume the element from the MUIA list.
                        index++;
                    }  // b2 is not an inner button

                    if ((addToList == true) && (b1.CurrentIsEnabled == 1))
                    {
                        // If found the outer border of a button and the button is active, then add it to list.
                        left = b1.CurrentBoundingRectangle.left;
                        top = b1.CurrentBoundingRectangle.top;
                        right = b1.CurrentBoundingRectangle.right;
                        bottom = b1.CurrentBoundingRectangle.bottom;

                        // VERY IMPORTANT!!!  The index value being saved is the index position in the
                        //                    original automationArray. We must be able to access this
                        //                    element in order to invoke its pattern (i.e. click on element).
                        automationElementList.TryAdd(index, b1);
                        clickableElements.Add(new ElementProperties(index, left, top, right, bottom));
                    }
                }   // if b1 is an outer button
            }  // while

            return clickableElements;
        }

        /* ++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++ */

        public List<ElementProperties> GetRemoteDisplayedElements()
        {
            List<ElementProperties> clickableElements;

            // Fetch array of all the GUI elements shown on the remote display using Microsoft's UIA library.
            automationElementArray = GetAutomationElementArray();

            // Check returned value and return empty list if MUIA failed to access remote app.
            if (automationElementArray.Length == 0)
            {
                clickableElements = null;
            }
            else
            {
                // Filter array into generic list of clickable elements that user can select in remote app.
                clickableElements = FilterAutomationElementArray(automationElementArray);
            }

            return clickableElements;
        }

        /* ++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++ */

        public void SelectElement(int elementIndex)
        {
            // Fetch the element from the unfiltered list
            // IUIAutomationElement element = automationElementArray.GetElement(elementIndex);
            IUIAutomationElement element;
            if(!automationElementList.TryGetValue(elementIndex, out element)) return;
             //System.Windows.Point point = element.GetClickablePoint();
             //System.Windows.Point point = new Point(100.0, 100.0);
             //Mouse.Move((int)point.X, (int)point.Y);
             //Mouse.Click(MouseButton.Left);

            int pattern = 10000;    // Value of UIA_InvokePatternId

            IUIAutomationInvokePattern ip;

            ip = (IUIAutomationInvokePattern)(element.GetCurrentPattern(pattern));

            //ip = element.GetCurrentPattern(InvokePattern.Pattern) as InvokePattern;
            //ip = element.GetCurrentPattern(UIA_InvokePatternId) as InvokePattern;
            if(ip == null)
            {
                Logger.Info(this, "invoke pattern is null");
            }
            ip.Invoke();

        }

        /* ++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++ */

        public bool CheckEmpowerIsAlive()
        {

            IUIAutomationElementArray automationArray;

            IUIAutomation automation = new CUIAutomation();
            IUIAutomationElement rootElement = automation.GetRootElement();

            if (rootElement != null)
            {
                IUIAutomationCondition condition = automation.CreatePropertyCondition(30005, "Empower™");

                // Get the parent element of all the children elements of the remote application.
                IUIAutomationElement appElement = rootElement.FindFirst(TreeScope.TreeScope_Children, condition);

                if (appElement != null)
                    return true;
            }
            return false;
        }

        private Process GetEmpowerProcess()
        {
            var pList = System.Diagnostics.Process.GetProcesses();
            foreach (Process p1 in pList)
            {
                Console.WriteLine(p1.Id + " " + p1.ProcessName);
                if (p1.MainWindowTitle != null && p1.MainWindowTitle.StartsWith("Empower™"))
                {
                    //ShowWindow(p1.MainWindowHandle, 3);
                    return p1;
                }
            }
            return null;
        }

        
    }
    public struct DisplayProperties
    {
        public int left;
        public int top;
        public int right;
        public int bottom;

        public DisplayProperties(int l, int t, int r, int b)
        {
            left = l;
            top = t;
            right = r;
            bottom = b;
        }
    }
}
