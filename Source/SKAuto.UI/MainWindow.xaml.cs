using SKAuto.UI.ViewModels;
using System.Windows;
using System.Windows.Input;

namespace SKAuto.UI
{
    public partial class MainWindow : Window
    {
        public MainWindow(MainViewModel viewModel)
        {
            InitializeComponent();
            DataContext = viewModel;
            Application.Current.MainWindow.WindowState = WindowState.Maximized;
            this.PreviewKeyDown += MainWindow_PreviewKeyDown;
        } 
        private void MainWindow_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
            {
                // Find the "New Work Order" button (by its Command binding) and execute its command
                var viewModel = DataContext as MainViewModel;
                if (viewModel?.CreateWorkOrderCommand?.CanExecute(null) == true)
                {
                    viewModel.CreateWorkOrderCommand.Execute(null);
                    e.Handled = true; // Prevent other handlers from processing
                }
            }
        }
    }
    }
   