using SKAuto.Core.Interfaces;
using SKAuto.UI.ViewModels;
using System;
using System.ComponentModel;
using System.Drawing;
using System.Windows;
using System.Windows.Input;
using WinForms = System.Windows.Forms;

namespace SKAuto.UI
{
    public partial class MainWindow : Window
    {
        private WinForms.NotifyIcon _trayIcon;
        private bool _isExiting = false;

        public MainWindow(MainViewModel viewModel)
        {
            InitializeComponent();
            DataContext = viewModel;
            System.Windows.Application.Current.MainWindow.WindowState = WindowState.Maximized;
            this.PreviewKeyDown += MainWindow_PreviewKeyDown;

            _trayIcon = new WinForms.NotifyIcon
            {
                Icon = System.Drawing.Icon.ExtractAssociatedIcon(Environment.GetCommandLineArgs()[0]),
                Text = "SKAuto Work Tracking",
                Visible = false,
                ContextMenuStrip = new WinForms.ContextMenuStrip()
            };

            var showItem = new WinForms.ToolStripMenuItem("Show");
            showItem.Click += (s, e) => ShowWindow();

            var exitItem = new WinForms.ToolStripMenuItem("Exit");
            exitItem.Click += (s, e) => ExitApplication();

            _trayIcon.ContextMenuStrip.Items.Add(showItem);
            _trayIcon.ContextMenuStrip.Items.Add(new WinForms.ToolStripSeparator());
            _trayIcon.ContextMenuStrip.Items.Add(exitItem);

            _trayIcon.DoubleClick += (s, e) => ShowWindow();
        }

        private void ShowWindow()
        {
            Show();
            WindowState = WindowState.Normal;
            _trayIcon.Visible = false;
            Activate();
        }

        private void ExitApplication()
        {
            _isExiting = true;
            _trayIcon.Visible = false;
            _trayIcon.Dispose();
            Close();
        }

        protected override void OnClosing(CancelEventArgs e)
        {
            if (_isExiting)
            {
                base.OnClosing(e);
                return;
            }

            if (App.CurrentUser != null)
            {
                e.Cancel = true;
                Hide();
                _trayIcon.Visible = true;
                App.GetService<ILoggingService>()?.LogInfo("Application minimized to system tray.");
            }
            else
            {
                base.OnClosing(e);
            }
        }

        protected override void OnClosed(EventArgs e)
        {
            _trayIcon?.Dispose();
            base.OnClosed(e);
        }

        private void MainWindow_PreviewKeyDown(object sender, System.Windows.Input.KeyEventArgs e)
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