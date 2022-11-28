using Starter.Calibration;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using ViewModels.Help;

namespace Starter.UIHelpers
{
    public class AttachedPropertyService
    {
        public static readonly DependencyProperty ChildWindowPrefereceProperty = DependencyProperty.RegisterAttached("ChildWindowPreferece", typeof(ChildWindowSettings), typeof(AttachedPropertyService), new FrameworkPropertyMetadata(OnChildWindowPreferenceChange));
        public static readonly DependencyProperty WindowSizeProperty = DependencyProperty.RegisterAttached("WindowSize", typeof(WindowSizeProperties), typeof(AttachedPropertyService), new FrameworkPropertyMetadata(OnChildWindowSizeChange));

       

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
                        windowCollection.Add(window, childWindow);
                    }
                    childWindow.DataContext = childWindowPref.DataContext;
                    childWindow.Visibility = Visibility.Visible;
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
