using Microsoft.Extensions.DependencyInjection;
using SKAuto.Core.Interfaces;
using SKAuto.UI.ViewModels;
using System;
using System.Windows;

namespace SKAuto.UI.Views
{
    public partial class MainWindow : Window
    {
        public MainWindow(MainViewModel viewModel)
        {
            try
            {
                var logger = App.GetService<ILoggingService>();
                logger.LogInfo("MainWindow constructor entered");

                
                DataContext = viewModel;

                logger.LogInfo("MainWindow constructor finished");

                this.Loaded += async (s, e) =>
                {
                    logger.LogInfo("MainWindow Loaded event started");
                    try
                    {
                        // Any async initialization that might throw
                        await viewModel.LoadTodayWorkCommand.ExecuteAsync(null);
                        logger.LogInfo("MainWindow Loaded event completed");
                    }
                    catch (Exception ex)
                    {
                        logger.LogError("Exception in Loaded event", ex);
                        MessageBox.Show($"Error during window load: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                    }
                };
                this.Closed += (s, e) => logger.LogInfo("MainWindow Closed event fired");
            }
            catch (Exception ex)
            {
                var logger = App.GetService<ILoggingService>();
                logger.LogError("MainWindow constructor failed", ex);
                throw;
            }
        }
    }
}