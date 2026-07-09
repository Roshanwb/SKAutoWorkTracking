using SKAuto.Core.Interfaces;
using SKAuto.UI.ViewModels;
using System;
using System.ComponentModel;
using System.Drawing;
using System.IO;
using System.Reflection;
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
            VersionTextBlock.Text = $"{Assembly.GetExecutingAssembly().GetName().Version.ToString()} ";
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

            // ---- CREATE BACKUP BEFORE EXIT ----
            try
            {
                if (App.CurrentUser != null)
                {
                    var logger = App.GetService<ILoggingService>();
                    logger?.LogInfo("Creating backup on exit (from system tray)...");

                    var backupService = App.GetService<IBackupService>();
                    var backupFolder = Path.Combine(
                        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                        "SKAuto",
                        "Backups");
                    var backupPath = backupService.BackupDatabaseAsync(backupFolder).GetAwaiter().GetResult();
                    logger?.LogInfo($"Backup created: {backupPath}");
                }
                else
                {
                    var logger = App.GetService<ILoggingService>();
                    logger?.LogInfo("No user logged in – skipping backup on exit.");
                }
            }
            catch (Exception ex)
            {
                var logger = App.GetService<ILoggingService>();
                logger?.LogError("Backup on exit failed", ex);
            }
            // ---------------------------------

            // Now shut down the application
            System.Windows.Application.Current.Shutdown();
        }

        protected override void OnClosing(CancelEventArgs e)
        {
            if (_isExiting)
            {
                base.OnClosing(e);
                return;
            }

            // Minimize to tray if logged in
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