
using System.IO;
using System.Runtime.InteropServices;
using System.Windows;
using ViewModels;

namespace Starter
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        private static readonly int SM_CONVERTIBLESLATEMODE = 0x2003;
        private static readonly int SM_TABLETPC = 0x56;
        private bool _isInTabletMode=false;
        public MainWindow()
        {
            InitializeComponent();
           var mainVM = new MainViewModel(CloseWindow);
            this.DataContext = mainVM;
            _isInTabletMode = QueryTabletMode();
            if(_isInTabletMode)
            {
                this.WindowState = WindowState.Maximized;
                mainVM.IsInTablet = true;
            }

        }
        private void CloseWindow()
        {
            this.Close();
        }

        private void Button_Click(object sender, RoutedEventArgs e)
        {
            this.WindowState = WindowState.Minimized;
        }
   
        private static bool QueryTabletMode()
        {
            return File.Exists(@"C:\Windows\OEM\PRCPlatformLib.dll");
        }
    }
}
