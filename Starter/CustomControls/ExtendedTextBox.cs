using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using CoreServices;


namespace Starter.CustomControls
{

    public class ExtendedTextBox : TextBox
    {
        Regex alphaNumericRegex;
        public static readonly DependencyProperty OnTabletProperty =    DependencyProperty.Register("OnTablet", typeof(bool), typeof(ExtendedTextBox),new FrameworkPropertyMetadata(false,FrameworkPropertyMetadataOptions.BindsTwoWayByDefault));

        public bool OnTablet
        {
            get { return (bool)GetValue(OnTabletProperty); }   
            set { SetValue(OnTabletProperty, value); }
        }
        public ExtendedTextBox()
        {
            alphaNumericRegex = new Regex("^[a-zA-Z0-9-]*$");
            this.GotTouchCapture += ExtendedTextBox_GotTouchCapture;
            GotFocus += ExtendedTextBox_GotFocus;
            this.PreviewTextInput += ExtendedTextBox_PreviewTextInput;
        }

        private void ExtendedTextBox_PreviewTextInput(object sender, System.Windows.Input.TextCompositionEventArgs e)
        {
            if(alphaNumericRegex.IsMatch(e.Text))
            {
                e.Handled = false;
            }
            else
            {
                e.Handled = true;
            }
        }

        private void ExtendedTextBox_GotFocus(object sender, RoutedEventArgs e)
        { 
            try
            {

                if (OnTablet)
                {
                    OpenVirtualKeyBoard();
                }


            }
            catch (Exception ex)
            {
                Logger.Error(this, ex.Message);
            }
        }

        private void ExtendedTextBox_GotTouchCapture(object? sender, System.Windows.Input.TouchEventArgs e)
        {
            try
            {

                if (OnTablet)
                {
                    OpenVirtualKeyBoard();
                }
               
            }
            catch (Exception ex)
            {
                Logger.Error(this,ex.Message);
            }

        }

        private void OpenVirtualKeyBoard()
        {
            Logger.Info(this,"opening keyboard");
            System.Diagnostics.Process process = new System.Diagnostics.Process();
            process.StartInfo.UseShellExecute = true;
            process.StartInfo.CreateNoWindow = true;
            process.StartInfo.FileName = "osk.exe";//@"C:\Program Files\Common Files\Microsoft Shared\Ink\TabTip.exe";
            process.StartInfo.Arguments = "";
            process.StartInfo.WorkingDirectory = "c:\\";

            process.Start();
        }

    }
}
