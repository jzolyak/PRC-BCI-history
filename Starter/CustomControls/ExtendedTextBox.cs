using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using Windows.Devices.Input;
using CoreServices;


namespace Starter.CustomControls
{

    public class ExtendedTextBox : TextBox
    {


        public ExtendedTextBox()
        {
            //this.GotTouchCapture += ExtendedTextBox_GotTouchCapture;
            //GotFocus += ExtendedTextBox_GotFocus;
        }

        private void ExtendedTextBox_GotFocus(object sender, RoutedEventArgs e)
        {
            try
            {

              
                System.Diagnostics.Process process = new System.Diagnostics.Process();
                process.StartInfo.UseShellExecute = true;
                process.StartInfo.CreateNoWindow = true;
                process.StartInfo.FileName = @"C:\Program Files\Common Files\Microsoft Shared\Ink\TabTip.exe";
                process.StartInfo.Arguments = "";
                process.StartInfo.WorkingDirectory = "c:\\";

                process.Start();

                //Wait for handle to become available             
                //                SetWindowPos(process.MainWindowHandle,
                //this.Handle, // Parent Window
                //this.Left, // Keypad Position X
                //this.Top + 20, // Keypad Position Y
                //panelButtons.Width, // Keypad Width
                //panelButtons.Height, // Keypad Height
                //SWP_SHOWWINDOW | SWP_NOZORDER); // Show Window and Place on Top
                //                SetForegroundWindow(process.MainWindowHandle);
               // [DllImport("user32.dll", EntryPoint = "SetWindowPos")]
               // public static extern IntPtr SetWindowPos(IntPtr hWnd, int hWndInsertAfter, int x, int Y, int cx, int cy, int wFlags);

            }
            catch (Exception ex)
            {
                Logger.Error(this,ex.Message);
            }
        }

        private void ExtendedTextBox_GotTouchCapture(object? sender, System.Windows.Input.TouchEventArgs e)
        {
            try
            {

               
 System.Diagnostics.Process process = new System.Diagnostics.Process();
                process.StartInfo.UseShellExecute = true;
                process.StartInfo.CreateNoWindow = true;
                process.StartInfo.FileName = @"C:\Program Files\Common Files\Microsoft Shared\Ink\TabTip.exe";
                process.StartInfo.Arguments = "";
                process.StartInfo.WorkingDirectory = "c:\\";

                process.Start();

                //Wait for handle to become available
                //while (process.MainWindowHandle == IntPtr.Zero)
                  //  Task.Delay(10).Wait();

               
            }
            catch (Exception ex)
            {
                Logger.Error(this,ex.Message);
            }

        }

    }
}
