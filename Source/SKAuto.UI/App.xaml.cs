using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using SKAuto.Core.Interfaces;
using SKAuto.Data;
using SKAuto.Data.Database;
using SKAuto.Export.Excel;
using SKAuto.Export.Pdf;
using SKAuto.Import.Parsers;
using SKAuto.Import.Validators;
using SKAuto.UI.ViewModels;
using System.Windows;

namespace SKAuto.UI
{
    public partial class App : Application
    {
        private readonly IHost _host;

        public App()
        {
            _host = Host.CreateDefaultBuilder()
                .ConfigureServices((context, services) =>
                {
                    // Database
                    services.AddSingleton<DatabaseContext>();
                    services.AddSingleton<DatabaseInitializer>();


                    // ViewModels
                    services.AddSingleton<MainViewModel>();
                    services.AddTransient<WorkOrderViewModel>();

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
                    //services.AddTransient<ImportViewModel>();
                    //services.AddTransient<ReportsViewModel>();

                    // Services
                    //services.AddScoped<DataService>();
                    //services.AddScoped<ImportService>();
                    //services.AddScoped<ReportService>();

                    // Repositories – already handled via UnitOfWork
                    services.AddScoped<IUnitOfWork, UnitOfWork>();
                    services.AddScoped<DatabaseContext>();
                    services.AddScoped<DatabaseInitializer>();

                    // Validators
                    services.AddScoped<IChassisValidator, ChassisValidator>();
                    services.AddScoped<IEODValidationService, EODValidationService>();

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