using CoreServices;
using Starter.Calibration;
using Starter.ValidationRules;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Media;
using ViewModels.Help;

namespace Starter.UIHelpers
{
    public class AttachedPropertyService
    {
        public static readonly DependencyProperty ChildWindowPrefereceProperty = DependencyProperty.RegisterAttached("ChildWindowPreferece", typeof(ChildWindowSettings), typeof(AttachedPropertyService), new FrameworkPropertyMetadata(OnChildWindowPreferenceChange));
        public static readonly DependencyProperty WindowSizeProperty = DependencyProperty.RegisterAttached("WindowSize", typeof(WindowSizeProperties), typeof(AttachedPropertyService), new FrameworkPropertyMetadata(OnChildWindowSizeChange));
        public static readonly DependencyProperty WindowTopMostProperty = DependencyProperty.RegisterAttached("WindowTopMost", typeof(WindowTopMost), typeof(AttachedPropertyService), new FrameworkPropertyMetadata(OnWindowTopMostChange));
        public static readonly DependencyProperty ValidationsProperty = DependencyProperty.RegisterAttached("Validations", typeof(List<Rule>), typeof(AttachedPropertyService), new FrameworkPropertyMetadata(OnValidationsChange));

        private static void OnValidationsChange(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            try
            {
                if (e.NewValue == null) return;
                if (d == null || !(d is TextBox)) return;
                var textBox = (TextBox)d;
                var binding = BindingOperations.GetBinding(textBox, TextBox.TextProperty);
                binding.ValidationRules.Clear();
                var validations = (List<Rule>)e.NewValue;
                if (validations == null ||  validations.Count == 0) return;
                foreach(var validation in validations)
                {
                    if (validation == null || string.IsNullOrEmpty(validation.RegExString)) continue;

                    binding.ValidationRules.Add(new RegExValidationRule(validation.RegExString, validation.ErrorMsg));
                }
            }
            catch (Exception ex)
            {
                Logger.Error("AttachedPropertyService", ex.ToString());
            }

        }

        private static void OnWindowTopMostChange(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            try
            {
                if (e.NewValue == null) return;
                if (d == null || !(d is Window)) return;
                var window = (Window)d;
                if (e.NewValue == null) return;
                var topmost = (WindowTopMost)e.NewValue;
                if(topmost!=null)
                {
                    window.Topmost = false;
                    window.Topmost = true;
                    window.Dispatcher.BeginInvoke(new Action(() =>
                    window.Activate()));
                }
            }
            catch(Exception ex)
            {
                Logger.Error("AttachedPropertyService", ex.ToString());
            }
        }

        // Declare a get accessor method.
        public static WindowTopMost GetWindowTopMost(UIElement target) =>
            (WindowTopMost)target.GetValue(WindowTopMostProperty);

        // Declare a set accessor method.
        public static void SetWindowTopMost(UIElement target, WindowTopMost value) =>
            target.SetValue(WindowTopMostProperty, value);

        private static Dictionary<Window, Window> windowCollection = new Dictionary<Window, Window>();
        private static void OnChildWindowPreferenceChange(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            try
            {
                if (e.NewValue == null) return;
                if (d == null || !(d is Window)) return;
                var window = (Window)d;
                Window? childWindow = null;
                if (windowCollection.ContainsKey(window))
                {
                    childWindow = windowCollection.GetValueOrDefault(window);
                }
                var childWindowPref = (ChildWindowSettings)e.NewValue;
                if (childWindowPref.ShowWindow)
                {
                    if (childWindow == null)
                    {
                        childWindow = new CalibrationWindow();
                        childWindow.Background = new SolidColorBrush(System.Windows.Media.Color.FromArgb(1, 0, 0, 0));
                        windowCollection.Add(window, childWindow);
                        
                    }
                    childWindow.Topmost = true;
                    childWindow.DataContext = childWindowPref.DataContext;
                    childWindow.Visibility = Visibility.Visible;
                    
                }
                else
                {
                    if (childWindow != null)
                    {
                        childWindow.Close();
                        windowCollection.Remove(window);
                    }
                }
            }
            catch (Exception ex)
            {

            }
        }

        // Declare a get accessor method.
        public static ChildWindowSettings GetChildWindowPreferece(UIElement target) =>
            (ChildWindowSettings)target.GetValue(ChildWindowPrefereceProperty);

        // Declare a set accessor method.
        public static void SetChildWindowPreferece(UIElement target, ChildWindowSettings value) =>
            target.SetValue(ChildWindowPrefereceProperty, value);


        private static void OnChildWindowSizeChange(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            try
            {
                if (e.NewValue == null) return;
                if (d == null || !(d is Window)) return;
                var window = (Window)d;
                var windowsize = (WindowSizeProperties)e.NewValue;
                if(windowsize==null) return;
                window.Width = windowsize.Width;
                window.Height = windowsize.Height;
                window.Top = windowsize.Top;
                window.Left = windowsize.Left;
                
                window.UpdateLayout();
               

            }
            catch(Exception ex)
            {

            }
        }

        public static WindowSizeProperties GetWindowSize(UIElement target) =>
          (WindowSizeProperties)target.GetValue(WindowSizeProperty);

        // Declare a set accessor method.
        public static void SetWindowSize(UIElement target, WindowSizeProperties value) =>
            target.SetValue(WindowSizeProperty, value);
    }
}
