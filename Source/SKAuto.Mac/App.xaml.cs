using Microsoft.Extensions.DependencyInjection;
using OpenSilver; 
using SKAuto.Core.Interfaces;
using SKAuto.Core.Services;
using SKAuto.Data;
using SKAuto.Mac.Services;
using SKAuto.UI.ViewModels;
using SKAuto.UI.Views;
using System;
using System.Windows;

namespace SKAuto.Mac
{
    public partial class App : Application
    {
        private IServiceProvider? _serviceProvider;

        public App()
        {
            InitializeComponent();
        }

        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);

            var services = new ServiceCollection();

            // Core services (replace with your actual implementations)
            services.AddScoped<IUnitOfWork, UnitOfWork>();
            services.AddSingleton<ILoggingService, LoggingService>();

            // Mac platform services
            services.AddSingleton<IDispatcherService, MacDispatcherService>();
            services.AddSingleton<IMessageBoxService, MacMessageBoxService>();

            // ViewModels
            services.AddScoped<AccessoryManagementViewModel>();
            // services.AddScoped<MainViewModel>(); // if MainWindow uses it

            // Views
            services.AddScoped<MainWindow>();

            _serviceProvider = services.BuildServiceProvider();

            // Set the ServiceLocator
            ServiceLocator.SetProvider(_serviceProvider);

            var mainWindow = _serviceProvider.GetRequiredService<MainWindow>();
            mainWindow.Show();
        }
    }
}