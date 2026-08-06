using SKAuto.Core.Interfaces;
using SKAuto.UI.ViewModels;
using System.ComponentModel;
using System.Reflection;
using System.Windows;
using System.Windows.Input;
using Application = System.Windows.Application;
using KeyEventArgs = System.Windows.Input.KeyEventArgs;

namespace SKAuto.UI
{
    public partial class MainWindow : Window
    {
        public MainWindow(MainViewModel viewModel)
        {
            InitializeComponent();
            DataContext = viewModel;
            System.Windows.Application.Current.MainWindow.WindowState = WindowState.Maximized;
            this.PreviewKeyDown += MainWindow_PreviewKeyDown;
            VersionTextBlock.Text = $"{Assembly.GetExecutingAssembly().GetName().Version.ToString()} ";
        }

        /// <summary>
        /// Intercepts the window close event (X button or Alt+F4).
        /// Triggers the async shutdown flow (splash + sync + backup + exit).
        /// </summary>
        protected override void OnClosing(CancelEventArgs e)
        {
            // Cancel the immediate close so we can run the async shutdown
            e.Cancel = true;
            ((App)Application.Current).BeginShutdown();
            this.Hide(); // Hide the window while the shutdown process is running
            
        }

        private void MainWindow_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
            {
                var viewModel = DataContext as MainViewModel;
                if (viewModel?.CreateWorkOrderCommand?.CanExecute(null) == true)
                {
                    viewModel.CreateWorkOrderCommand.Execute(null);
                    e.Handled = true;
                }
            }
        }
    }
}