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
    public partial class App : Application
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
                    services.AddSingleton<MainWindow>();
                })
                .Build();
        }

        protected override async void OnStartup(StartupEventArgs e)
        {
            // CRITICAL: Set shutdown mode before any windows are created
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

                        // Fix culture if it contains description like "Français (fr-FR)"
                        string fixedLanguage = appConfig.Language;
                        if (!string.IsNullOrEmpty(fixedLanguage) && fixedLanguage.Contains("(") && fixedLanguage.Contains(")"))
                        {
                            int start = fixedLanguage.IndexOf('(') + 1;
                            int end = fixedLanguage.IndexOf(')');
                            if (start > 0 && end > start)
                            {
                                fixedLanguage = fixedLanguage.Substring(start, end - start);
                                appConfig.Language = fixedLanguage;
                                configService.SetAsync("AppConfig", appConfig).GetAwaiter().GetResult();
                                _logger.LogInfo($"Fixed culture from '{appConfig.Language}' to '{fixedLanguage}'");
                            }
                        }

                        try
                        {
                            var culture = new CultureInfo(fixedLanguage);
                            CultureInfo.DefaultThreadCurrentCulture = culture;
                            CultureInfo.DefaultThreadCurrentUICulture = culture;
                            _logger.LogInfo($"Culture set to {fixedLanguage}");
                        }
                        catch (CultureNotFoundException)
                        {
                            _logger.LogWarning($"Invalid culture '{fixedLanguage}', falling back to fr-FR");
                            CultureInfo.DefaultThreadCurrentCulture = new CultureInfo("fr-FR");
                            CultureInfo.DefaultThreadCurrentUICulture = new CultureInfo("fr-FR");
                            appConfig.Language = "fr-FR";
                            configService.SetAsync("AppConfig", appConfig).GetAwaiter().GetResult();
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError("Initialization failed", ex);
                        throw;
                    }
                },
                onComplete: () =>
                {
                    try
                    {
                        var mainWindow = _host.Services.GetRequiredService<MainWindow>();
                        // Assign as the main window so ShutdownMode works correctly
                        Current.MainWindow = mainWindow;
                        mainWindow.Show();
                        mainWindow.Activate();
                        mainWindow.Focus();
                        _logger.LogInfo("Main window shown and activated.");
                        Application.Current.MainWindow.WindowState = WindowState.Maximized;
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError("Failed to show main window", ex);
                        MessageBox.Show($"Fatal error:\n{ex.Message}\n\nCheck log at %APPDATA%\\SKAuto\\Logs",
                                        "Startup Failed", MessageBoxButton.OK, MessageBoxImage.Error);
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