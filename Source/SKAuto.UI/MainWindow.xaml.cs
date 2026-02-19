using System.Windows;
using SKAuto.UI.ViewModels;

namespace SKAuto.UI
{
    public partial class MainWindow : Window
    {
        public MainWindow(MainViewModel viewModel)
        {
            InitializeComponent();
            DataContext = viewModel;
        }
    }
}