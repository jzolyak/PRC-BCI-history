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

            int left;
            int top;
            int right;
            int bottom;

            int index = 0;
            int numElements = automationArray.Length;

            List<ElementProperties> clickableElements = new List<ElementProperties>();

			 automationElementList.Clear();							  
            while (index < numElements)
            {
                IUIAutomationElement b1 = automationArray.GetElement(index++);

                if (b1.CurrentLocalizedControlType == "button")
                {
                    IUIAutomationElement b2 = automationArray.GetElement(index++);

                    if (b2.CurrentLocalizedControlType == "button")
                    {
                        if (b1.CurrentIsEnabled == 1)
                        {
                            left = b1.CurrentBoundingRectangle.left;
                            top = b1.CurrentBoundingRectangle.top;
                            right = b1.CurrentBoundingRectangle.right;
                            bottom = b1.CurrentBoundingRectangle.bottom;

                            // VERY IMPORTANT!!!  The index value being saved is the index position in the
                            //                    original automationArray. We must be able to access this
                            //                    element in order to invoke its pattern (i.e. click on element).
                            automationElementList.TryAdd(index,b1);
                            clickableElements.Add(new ElementProperties(index, left, top, right, bottom));
                        }
                    }
                }
            }
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
