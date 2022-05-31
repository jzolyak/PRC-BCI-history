
using System.Windows;
using ViewModels;

namespace Starter
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        public MainWindow()
        {
            InitializeComponent();
            this.DataContext = new MainViewModel(CloseWindow);
        }
        private void CloseWindow()
        {
            this.Close();
        }
    }
}
