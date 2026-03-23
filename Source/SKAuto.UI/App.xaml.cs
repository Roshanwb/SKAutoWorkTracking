using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using SKAuto.Core.DTOs;          // For AppConfig
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

        public static User CurrentUser { get; set; }
        public static AppConfig CurrentConfig { get; private set; }

        public App()
        {
            _host = Host.CreateDefaultBuilder()
                .ConfigureServices((context, services) =>
                {
                    // Database
                    services.AddSingleton<DatabaseContext>();
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
            await _host.StartAsync();

            // Initialize database
            var initializer = _host.Services.GetRequiredService<DatabaseInitializer>();
            await initializer.InitializeAsync();

            // Load configuration
            var configService = _host.Services.GetRequiredService<IConfigurationService>();
            var appConfig = await configService.GetAsync<AppConfig>("AppConfig");
            if (appConfig == null)
            {
                // First run – create and save default config
                appConfig = new AppConfig();
                await configService.SetAsync("AppConfig", appConfig);
            }
            CurrentConfig = appConfig;

            // Set application culture based on saved language
            try
            {
                var culture = new CultureInfo(appConfig.Language);
                CultureInfo.DefaultThreadCurrentCulture = culture;
                CultureInfo.DefaultThreadCurrentUICulture = culture;
            }
            catch
            {
                // Fallback to French if invalid
                CultureInfo.DefaultThreadCurrentCulture = new CultureInfo("fr-FR");
                CultureInfo.DefaultThreadCurrentUICulture = new CultureInfo("fr-FR");
            }

            // Show main window
            var mainWindow = _host.Services.GetRequiredService<MainWindow>();
            mainWindow.Show();

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