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
        private Exception _initException;

        public static User CurrentUser { get; set; }
        public static AppConfig CurrentConfig { get; private set; }

        public App()
        {
            _host = Host.CreateDefaultBuilder()
                .ConfigureServices((context, services) =>
                {
                    // Database
                    services.AddTransient<DatabaseContext>();
                    services.AddSingleton<DatabaseInitializer>();
                    var dbPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "SKAuto", "SKAuto.db");
                    services.AddSingleton(dbPath);

                    // Unit of Work
                    services.AddScoped<IUnitOfWork, UnitOfWork>();

                    // Import/Export
                    services.AddScoped<IImportParser, PSAParser>();
                    services.AddScoped<IImportParser, ExcelParser>();
                    services.AddScoped<IImportParser, PdfParser>();
                    services.AddScoped<IValidationService, ImportValidator>();
                    services.AddScoped<IExportService, ExcelReportGenerator>();
                    services.AddScoped<PdfReportGenerator>();

                    // ViewModels
                    services.AddSingleton<MainViewModel>();
                    services.AddTransient<WorkOrderViewModel>();

                    // Services
                    services.AddScoped<IBackupService, BackupService>();
                    services.AddScoped<IGoogleDriveService, GoogleDriveService>();

                    // Validators
                    services.AddScoped<IChassisValidator, ChassisValidator>();
                    services.AddScoped<IEODValidationService, EODValidationService>();

                    // Logging
                    services.AddSingleton<ILoggingService, LoggingService>();

                    // Configuration
                    services.AddSingleton<IConfigurationService, JsonConfigurationService>();

                    // Main Window
                    services.AddSingleton<MainWindow>();
                })
                .Build();
        }

        protected override async void OnStartup(StartupEventArgs e)
        {
            // Start the host
            await _host.StartAsync();

            // Create splash screen with loading actions
            var splash = new Splash(
                loadResources: () =>
                {
                    // This runs in a background thread – do not touch UI here
                    try
                    {
                        // 1. Initialize database
                        var initializer = _host.Services.GetRequiredService<DatabaseInitializer>();
                        initializer.InitializeAsync().GetAwaiter().GetResult();

                        // 2. Load configuration
                        var configService = _host.Services.GetRequiredService<IConfigurationService>();
                        var appConfig = configService.GetAsync<AppConfig>("AppConfig").GetAwaiter().GetResult();
                        if (appConfig == null)
                        {
                            appConfig = new AppConfig();
                            configService.SetAsync("AppConfig", appConfig).GetAwaiter().GetResult();
                        }
                        CurrentConfig = appConfig;

                        // 3. Set application culture – with fallback for invalid strings
                        try
                        {
                            var culture = new CultureInfo(appConfig.Language);
                            CultureInfo.DefaultThreadCurrentCulture = culture;
                            CultureInfo.DefaultThreadCurrentUICulture = culture;
                        }
                        catch (CultureNotFoundException)
                        {
                            // Fallback to French (fr-FR) and fix config
                            CultureInfo.DefaultThreadCurrentCulture = new CultureInfo("fr-FR");
                            CultureInfo.DefaultThreadCurrentUICulture = new CultureInfo("fr-FR");
                            appConfig.Language = "fr-FR";
                            configService.SetAsync("AppConfig", appConfig).GetAwaiter().GetResult();
                        }
                        catch
                        {
                            // Fallback to French on any other error
                            CultureInfo.DefaultThreadCurrentCulture = new CultureInfo("fr-FR");
                            CultureInfo.DefaultThreadCurrentUICulture = new CultureInfo("fr-FR");
                        }
                    }
                    catch (Exception ex)
                    {
                        _initException = ex;
                        throw; // rethrow to be caught by splash
                    }
                },
                onComplete: () =>
                {
                    // This runs on UI thread after splash closes
                    if (_initException != null)
                    {
                        MessageBox.Show($"Startup failed: {_initException.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                        Current.Shutdown();
                        return;
                    }

                    try
                    {
                        var mainWindow = _host.Services.GetRequiredService<MainWindow>();
                        mainWindow.Show();
                    }
                    catch (Exception ex)
                    {
                        MessageBox.Show($"Failed to start main window: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                        Current.Shutdown();
                    }
                }
            );

            splash.Show();

            base.OnStartup(e);
        }

        protected override async void OnExit(ExitEventArgs e)
        {
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