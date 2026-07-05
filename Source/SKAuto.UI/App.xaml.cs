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
using SKAuto.UI.ViewModels;
using SKAuto.UI.Views;
using System;
using System.Globalization;
using System.IO;
using System.Threading.Tasks;
using System.Windows;

namespace SKAuto.UI
{
    public partial class App : System.Windows.Application
    {
        private readonly IHost _host;
        private ILoggingService _logger;

        public static User CurrentUser { get; set; }
        public static AppConfig CurrentConfig { get; private set; }

        public App()
        {
            _host = Host.CreateDefaultBuilder()
                .ConfigureServices((context, services) =>
                {
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
            _logger = _host.Services.GetRequiredService<ILoggingService>();
            _logger.LogInfo("Application starting...");

            var splash = new Splash(
                loadResources: () =>
                {
                    try
                    {
                        var initializer = _host.Services.GetRequiredService<DatabaseInitializer>();
                        initializer.InitializeAsync().GetAwaiter().GetResult();
                        _logger.LogInfo("Database initialized.");

                        var configService = _host.Services.GetRequiredService<IConfigurationService>();
                        var appConfig = configService.GetAsync<AppConfig>("AppConfig").GetAwaiter().GetResult();
                        if (appConfig == null)
                        {
                            appConfig = new AppConfig();
                            configService.SetAsync("AppConfig", appConfig).GetAwaiter().GetResult();
                            _logger.LogInfo("Created default configuration.");
                        }
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
                            _logger.LogInfo($"Culture set to {fixedLanguage}");
                        }
                        catch
                        {
                            _logger.LogWarning($"Invalid culture, falling back to fr-FR");
                            CultureInfo.DefaultThreadCurrentCulture = new CultureInfo("fr-FR");
                            CultureInfo.DefaultThreadCurrentUICulture = new CultureInfo("fr-FR");
                        }

                        // ---- GOOGLE DRIVE AUTO-AUTH ----
                        try
                        {
                            var driveSettings = configService.GetAsync<GoogleDriveSettings>("GoogleDrive").GetAwaiter().GetResult();
                            if (driveSettings != null && !string.IsNullOrEmpty(driveSettings.ClientId) && !string.IsNullOrEmpty(driveSettings.ClientSecret))
                            {
                                var driveService = _host.Services.GetRequiredService<IGoogleDriveService>();
                                bool authenticated = driveService.AuthenticateAsync(driveSettings).GetAwaiter().GetResult();
                                if (authenticated)
                                    _logger.LogInfo("Google Drive auto-authenticated successfully on startup.");
                                else
                                    _logger.LogWarning("Google Drive auto-authentication failed.");
                            }
                        }
                        catch (Exception ex)
                        {
                            _logger.LogWarning($"Google Drive auto-authentication error: {ex.Message}");
                        }
                        // ---- END AUTO-AUTH ----
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError("Resource loading failed", ex);
                        throw;
                    }
                },
                onComplete: () =>
                {
                    try
                    {
                        // Show login
                        User loggedInUser = null;
                        using (var scope = _host.Services.CreateScope())
                        {
                            var unitOfWork = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();
                            var logger = scope.ServiceProvider.GetRequiredService<ILoggingService>();
                            var loginVM = new LoginViewModel(unitOfWork, logger);
                            var loginView = new LoginView(loginVM);
                            if (loginView.ShowDialog() != true)
                            {
                                _logger.LogInfo("Login cancelled or failed. Exiting.");
                                Shutdown();
                                return;
                            }
                            loggedInUser = App.CurrentUser;
                        }

                        var mainWindow = _host.Services.GetRequiredService<MainWindow>();
                        if (mainWindow.DataContext is MainViewModel mainVM)
                        {
                            mainVM.SetCurrentUser(loggedInUser);
                        }

                        Current.MainWindow = mainWindow;
                        mainWindow.Show();
                        mainWindow.Activate();
                        mainWindow.Focus();
                        _logger.LogInfo("Main window shown and activated.");
                        System.Windows.Application.Current.MainWindow.WindowState = WindowState.Maximized;
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError("Failed to show main window", ex);
                        System.Windows.MessageBox.Show($"Fatal error: {ex.Message}", "Error", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Error);
                        Environment.Exit(1);
                    }
                },
                logger: _logger
            );

            splash.Show();
            base.OnStartup(e);
        }

        protected override async void OnExit(ExitEventArgs e)
        {
            _logger?.LogInfo("Application exiting.");

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
            else
            {
                _logger?.LogInfo("No user logged in – skipping backup on exit.");
            }

            await _host.StopAsync();
            _host.Dispose();
            base.OnExit(e);
        }

        public static T GetService<T>() where T : class
        {
            return ((App)Current)._host.Services.GetRequiredService<T>();
        }
    }
}