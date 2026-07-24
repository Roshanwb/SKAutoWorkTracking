using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using SKAuto.Core.DTOs;
using SKAuto.Core.Entities;
using SKAuto.Core.Interfaces;
using SKAuto.Core.Services;
using SKAuto.Data;
using SKAuto.Data.Database;
using SKAuto.Export.Excel;
using SKAuto.Export.Pdf;
using SKAuto.Import.Parsers;
using SKAuto.Import.Validators;
using SKAuto.UI.Localization;
using SKAuto.UI.Services;
using SKAuto.UI.ViewModels;
using SKAuto.UI.Views;
using System.Globalization;
using System.IO;
using System.Windows;

namespace SKAuto.UI
{
    public partial class App : System.Windows.Application
    {
        private GoogleDriveLockService? _lockService;
        private DatabaseSyncService? _syncService;
        private bool _syncError;
        private Splash? _splash;
        private bool _isShuttingDown;

        private readonly IHost _host;
        private ILoggingService _logger;
        private AppConfig _currentAppConfig;
        public static IServiceProvider? ServiceProvider { get; private set; }

        public static User CurrentUser { get; set; }
        public static AppConfig CurrentConfig { get; private set; }

        public App()
        {
            _host = Host.CreateDefaultBuilder()
                .ConfigureServices((context, services) =>
                {
                    services.AddSingleton<IDispatcherService, WindowsDispatcherService>();
                    services.AddSingleton<IMessageBoxService, WindowsMessageBoxService>();
                    services.AddTransient<DatabaseContext>();
                    services.AddSingleton<DatabaseInitializer>();
                    var dbPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "SKAuto", "SKAuto.db");
                    services.AddSingleton(dbPath);
                    services.AddScoped<IUnitOfWork, UnitOfWork>();
                    services.AddScoped<IImportParser, PSAParser>();
                    services.AddScoped<IImportParser, ExcelParser>();
                    services.AddScoped<IImportParser, PdfParser>();
                    services.AddScoped<IValidationService, ImportValidator>();
                    services.AddScoped<IExportService, ExcelReportGenerator>();
                    services.AddScoped<PdfReportGenerator>();
                    services.AddSingleton<MainViewModel>();
                    services.AddTransient<WorkOrderViewModel>();
                    services.AddScoped<IBackupService, BackupService>();
                    services.AddScoped<IGoogleDriveService, GoogleDriveService>();
                    services.AddScoped<IChassisValidator, ChassisValidator>();
                    services.AddScoped<IEODValidationService, EODValidationService>();
                    services.AddSingleton<ILoggingService, LoggingService>();
                    services.AddSingleton<IConfigurationService, JsonConfigurationService>();
                    services.AddSingleton<IEmailService, SmtpEmailService>();
                    services.AddSingleton<MainWindow>();
                })
                .Build();
        }

        protected override async void OnStartup(StartupEventArgs e)
        {
            ShutdownMode = ShutdownMode.OnMainWindowClose;

            await _host.StartAsync();
            ServiceLocator.SetProvider(_host.Services);
            _logger = _host.Services.GetRequiredService<ILoggingService>();
            _logger.LogInfo(LocalizationManager.Instance["ApplicationStarting"]);

            _splash = new Splash(
                loadResources: () =>
                {
                    try
                    {
                        var initializer = _host.Services.GetRequiredService<DatabaseInitializer>();
                        initializer.InitializeAsync().GetAwaiter().GetResult();
                        _logger.LogInfo(LocalizationManager.Instance["DatabaseInitialized"]);

                        var configService = _host.Services.GetRequiredService<IConfigurationService>();
                        var appConfig = configService.GetAsync<AppConfig>("AppConfig").GetAwaiter().GetResult();
                        if (appConfig == null)
                        {
                            appConfig = new AppConfig();
                            configService.SetAsync("AppConfig", appConfig).GetAwaiter().GetResult();
                            _logger.LogInfo(LocalizationManager.Instance["CreatedDefaultConfiguration"]);
                        }
                        _currentAppConfig = appConfig;
                        CurrentConfig = appConfig;

                        string fixedLanguage = appConfig.Language;
                        if (!string.IsNullOrEmpty(fixedLanguage) && fixedLanguage.Contains("("))
                        {
                            int start = fixedLanguage.IndexOf('(') + 1;
                            int end = fixedLanguage.IndexOf(')');
                            if (start > 0 && end > start)
                                fixedLanguage = fixedLanguage.Substring(start, end - start);
                        }
                        try
                        {
                            var culture = new CultureInfo(fixedLanguage);
                            CultureInfo.DefaultThreadCurrentCulture = culture;
                            CultureInfo.DefaultThreadCurrentUICulture = culture;
                            LocalizationManager.Instance.CurrentCulture = culture;
                            _logger.LogInfo($"Culture set to {fixedLanguage}");
                        }
                        catch
                        {
                            var culture = new CultureInfo("fr-FR");
                            CultureInfo.DefaultThreadCurrentCulture = culture;
                            CultureInfo.DefaultThreadCurrentUICulture = culture;
                            LocalizationManager.Instance.CurrentCulture = culture;
                            _logger.LogWarning($"Invalid culture, falling back to fr-FR");
                        }

                        // Google Drive auto-auth
                        try
                        {
                            var driveSettings = configService.GetAsync<GoogleDriveSettings>("GoogleDrive").GetAwaiter().GetResult();
                            if (driveSettings != null && !string.IsNullOrEmpty(driveSettings.ClientId) && !string.IsNullOrEmpty(driveSettings.ClientSecret))
                            {
                                var driveService = _host.Services.GetRequiredService<IGoogleDriveService>();
                                bool authenticated = driveService.AuthenticateAsync(driveSettings).GetAwaiter().GetResult();
                                if (authenticated)
                                    _logger.LogInfo(LocalizationManager.Instance["GoogleDriveAutoAuthenticatedSuccessfully"]);
                                else
                                    _logger.LogWarning(LocalizationManager.Instance["GoogleDriveAutoAuthenticationFailed"]);
                            }
                        }
                        catch (Exception ex)
                        {
                            _logger.LogWarning($"Google Drive auto-authentication error: {ex.Message}");
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(LocalizationManager.Instance["ResourceLoadingFailed"], ex);
                        throw;
                    }
                },
                onComplete: async () =>
                {
                    _logger.LogInfo("onComplete delegate started.");

                    try
                    {
                        _splash?.UpdateStatus("Initializing cloud sync...", 0);

                        var driveService = _host.Services.GetRequiredService<IGoogleDriveService>();
                        var logger = _host.Services.GetRequiredService<ILoggingService>();
                        var dbPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "SKAuto", "SKAuto.db");

                        _lockService = new GoogleDriveLockService(driveService, logger);
                        _syncService = new DatabaseSyncService(driveService, logger, dbPath);

                        // 1. Check connectivity
                        _splash?.UpdateStatus("Checking internet connection...", 10);
                        bool connected = await driveService.IsConnectedAsync();
                        if (!connected)
                        {
                            var result = System.Windows.MessageBox.Show(
                                LocalizationManager.Instance["Sync_InternetRequired"],
                                LocalizationManager.Instance["Sync_ConnectionError"],
                                MessageBoxButton.OKCancel,
                                MessageBoxImage.Warning);

                            if (result == MessageBoxResult.Cancel)
                            {
                                Shutdown();
                                return;
                            }
                            connected = await driveService.IsConnectedAsync();
                            if (!connected)
                            {
                                System.Windows.MessageBox.Show(
                                    LocalizationManager.Instance["Sync_InternetUnavailable"],
                                    LocalizationManager.Instance["Sync_ConnectionError"],
                                    MessageBoxButton.OK,
                                    MessageBoxImage.Error);
                                Shutdown();
                                return;
                            }
                        }

                        // 2. Acquire lock with force unlock option
                        _splash?.UpdateStatus("Acquiring cloud lock...", 20);
                        var lockResult = await _lockService.TryAcquireLockAsync(
                            Environment.MachineName,
                            Environment.UserName);

                        if (lockResult.Result == LockResult.LockedByOtherUser)
                        {
                            var forceChoice = System.Windows.MessageBox.Show(
                                string.Format(LocalizationManager.Instance["Sync_LockActive"], lockResult.MachineName, lockResult.UserName) +
                                "\n\n" + LocalizationManager.Instance["Sync_ForceUnlockQuestion"],
                                LocalizationManager.Instance["Sync_LockTitle"],
                                MessageBoxButton.YesNo,
                                MessageBoxImage.Warning);

                            if (forceChoice == MessageBoxResult.Yes)
                            {
                                lockResult = await _lockService.TryAcquireLockAsync(
                                    Environment.MachineName,
                                    Environment.UserName,
                                    forceUnlock: true);
                                if (lockResult.Result != LockResult.Success)
                                {
                                    System.Windows.MessageBox.Show(
                                        LocalizationManager.Instance["Sync_ForceUnlockFailed"],
                                        LocalizationManager.Instance["Sync_ErrorTitle"],
                                        MessageBoxButton.OK,
                                        MessageBoxImage.Error);
                                    Shutdown();
                                    return;
                                }
                            }
                            else
                            {
                                Shutdown();
                                return;
                            }
                        }

                        if (lockResult.Result == LockResult.StaleLockDetected)
                        {
                            var userChoice = System.Windows.MessageBox.Show(
                                string.Format(LocalizationManager.Instance["Sync_StaleLockPrompt"], lockResult.MachineName),
                                LocalizationManager.Instance["Sync_LockTitle"],
                                MessageBoxButton.YesNo,
                                MessageBoxImage.Question);

                            if (userChoice == MessageBoxResult.Yes)
                            {
                                lockResult = await _lockService.TryAcquireLockAsync(
                                    Environment.MachineName,
                                    Environment.UserName,
                                    forceUnlock: true);
                            }
                            else
                            {
                                Shutdown();
                                return;
                            }
                        }

                        if (lockResult.Result != LockResult.Success)
                        {
                            System.Windows.MessageBox.Show(
                                LocalizationManager.Instance["Sync_LockFailed"],
                                LocalizationManager.Instance["Sync_ErrorTitle"],
                                MessageBoxButton.OK,
                                MessageBoxImage.Error);
                            Shutdown();
                            return;
                        }

                        // 3. Start heartbeat
                        _lockService.StartHeartbeat();

                        // 4. Database sync
                        _splash?.UpdateStatus("Synchronizing database...", 40);
                        var syncResult = await _syncService.SyncOnStartupAsync(Environment.MachineName);

                        if (syncResult.Result == SyncResult.ConflictDetected)
                        {
                            _splash?.UpdateStatus("Conflict detected. Waiting for user decision...", 70);
                            var userChoice = System.Windows.MessageBox.Show(
                                string.Format(LocalizationManager.Instance["Sync_ConflictMessage"],
                                    syncResult.LocalVersion,
                                    syncResult.RemoteVersion,
                                    syncResult.LocalBackupPath),
                                LocalizationManager.Instance["Sync_ConflictTitle"],
                                MessageBoxButton.YesNo,
                                MessageBoxImage.Warning);

                            if (userChoice == MessageBoxResult.Yes)
                            {
                                await _syncService.ResolveConflictAsync(useRemote: true);
                            }
                            else
                            {
                                await _syncService.ResolveConflictAsync(useRemote: false);
                            }
                        }
                        else if (syncResult.Result != SyncResult.Success)
                        {
                            System.Windows.MessageBox.Show(
                                string.Format(LocalizationManager.Instance["Sync_Error"], syncResult.Message),
                                LocalizationManager.Instance["Sync_ErrorTitle"],
                                MessageBoxButton.OK,
                                MessageBoxImage.Error);
                            _syncError = true;
                            Shutdown();
                            return;
                        }

                        _splash?.UpdateStatus("Sync complete. Loading application...", 90);

                        // --- Login ---
                        User loggedInUser = null;
                        using (var scope = _host.Services.CreateScope())
                        {
                            var unitOfWork = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();
                            var logger2 = scope.ServiceProvider.GetRequiredService<ILoggingService>();
                            var loginVM = new LoginViewModel(unitOfWork, logger2);
                            var loginView = new LoginView(loginVM);
                            if (loginView.ShowDialog() != true)
                            {
                                _logger.LogInfo(LocalizationManager.Instance["LoginCancelledOrFailed"]);
                                Shutdown();
                                return;
                            }
                            loggedInUser = App.CurrentUser;
                        }

                        // Apply theme
                        if (_currentAppConfig != null)
                        {
                            ApplicationThemeManager.ApplyTheme(_currentAppConfig.Theme ?? "Light");
                            _logger.LogInfo($"Applied theme: {_currentAppConfig.Theme ?? "Light"}");
                        }

                        var mainWindow = _host.Services.GetRequiredService<MainWindow>();
                        if (mainWindow.DataContext is MainViewModel mainVM)
                        {
                            mainVM.SetCurrentUser(loggedInUser);
                        }

                        Current.MainWindow = mainWindow;
                        _splash?.UpdateStatus("Starting application...", 100);
                        mainWindow.Show();
                        mainWindow.Activate();
                        mainWindow.Focus();
                        _logger.LogInfo(LocalizationManager.Instance["MainWindowShownAndActivated"]);
                        System.Windows.Application.Current.MainWindow.WindowState = WindowState.Maximized;
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError("Cloud sync failed during startup", ex);
                        System.Windows.MessageBox.Show(
                            string.Format(LocalizationManager.Instance["Sync_Error"], ex.Message),
                            LocalizationManager.Instance["Sync_ErrorTitle"],
                            MessageBoxButton.OK,
                            MessageBoxImage.Error);
                        Shutdown();
                    }
                },
                logger: _logger
            );

            _splash.Show();
            base.OnStartup(e);
        }

        // --- Public method for MainWindow to trigger shutdown ---
        public async void BeginShutdown()
        {
            if (_isShuttingDown) return;
            _isShuttingDown = true;

            // Show the splash screen again (if not already visible)
            if (_splash != null)
            {
                if (!_splash.IsVisible)
                {
                    _splash.Show();
                    _splash.Topmost = true; // Ensure it's on top
                    _splash.Activate();
                }
                _splash.UpdateStatus("Shutting down...", 0);
            }

            await PerformShutdownAsync();
        }

        private async Task PerformShutdownAsync()
        {
            try
            {
                if (_splash != null)
                {
                    _splash.UpdateStatus("Synchronizing database...", 20);
                }

                bool syncSuccess = false;

                // Perform exit sync (if no sync error occurred)
                if (!_syncError && _syncService != null && _lockService != null)
                {
                    try
                    {
                        await _syncService.SyncOnExitAsync(Environment.MachineName);
                        await _lockService.ReleaseLockAsync();
                        syncSuccess = true;
                        _logger.LogInfo("Exit sync completed successfully.");
                    }
                    catch (Exception ex)
                    {
                        _logger?.LogError("Exit sync failed", ex);
                        if (_splash != null)
                        {
                            _splash.UpdateStatus($"Sync failed: {ex.Message}", 50);
                            await Task.Delay(1500);
                        }
                    }
                }

                if (_splash != null)
                {
                    _splash.UpdateStatus(syncSuccess ? "Sync complete. Closing..." : "Closing with errors...", 80);
                    await Task.Delay(500);
                }

                // Backup (existing logic)
                if (App.CurrentUser != null)
                {
                    try
                    {
                        var backupService = _host.Services.GetRequiredService<IBackupService>();
                        var backupFolder = Path.Combine(
                            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                            "SKAuto",
                            "Backups");
                        var backupPath = await backupService.BackupDatabaseAsync(backupFolder);
                        _logger.LogInfo($"Automatic backup created on exit: {backupPath}");
                    }
                    catch (Exception ex)
                    {
                        _logger?.LogError("Automatic backup failed on exit", ex);
                    }
                }

                // Clean up host and lock service
                await _host.StopAsync();
                _host.Dispose();
                (_lockService as IDisposable)?.Dispose();

                if (_splash != null)
                {
                    _splash.UpdateStatus("Done.", 100);
                    await Task.Delay(300);
                    _splash.Close();
                }

                // Show final confirmation message
                if (syncSuccess)
                {
                    System.Windows.MessageBox.Show(
                        LocalizationManager.Instance["Sync_ExitSuccess"],
                        LocalizationManager.Instance["Sync_SuccessTitle"],
                        MessageBoxButton.OK,
                        MessageBoxImage.Information);
                }
                else
                {
                    System.Windows.MessageBox.Show(
                        LocalizationManager.Instance["Sync_ExitErrorGeneral"],
                        LocalizationManager.Instance["Sync_ErrorTitle"],
                        MessageBoxButton.OK,
                        MessageBoxImage.Warning);
                }

                // Force exit to ensure process terminates
                Environment.Exit(0);
            }
            catch (Exception ex)
            {
                _logger?.LogError("Shutdown failed", ex);
                System.Windows.MessageBox.Show($"Shutdown error: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                Environment.Exit(1);
            }
        }

        protected override async void OnExit(ExitEventArgs e)
        {
            if (!_isShuttingDown)
            {
                BeginShutdown();
            }
            base.OnExit(e);
        }

        public static T GetService<T>() where T : class
        {
            return ((App)Current)._host.Services.GetRequiredService<T>();
        }
    }
}