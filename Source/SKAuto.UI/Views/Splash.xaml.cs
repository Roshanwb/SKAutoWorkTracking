using SKAuto.Core.Interfaces;
using System;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media.Animation;

namespace SKAuto.UI.Views
{
    public partial class Splash : Window
    {
        private readonly Action _loadResources;
        private readonly Func<Task> _onComplete;
        private readonly ILoggingService _logger;
        private Exception _loadException;

        public Splash(Action loadResources, Func<Task> onComplete, ILoggingService logger = null)
        {
            InitializeComponent();
            _loadResources = loadResources;
            _onComplete = onComplete;
            _logger = logger;
            Loaded += Splash_Loaded;
        }

        public void UpdateStatus(string message, int progress = 0)
        {
            Dispatcher.Invoke(() =>
            {
                StatusText.Text = message;
            });
        }

        private async void Splash_Loaded(object sender, RoutedEventArgs e)
        {
            await LoadResourcesAsync();
        }

        private async Task LoadResourcesAsync()
        {
            try
            {
                StatusText.Text = "Loading configuration...";
                if (_logger != null) _logger.LogInfo("Splash: loading resources...");
                await Task.Delay(100);

                await Task.Run(() =>
                {
                    _loadResources?.Invoke();
                });

                if (_logger != null) _logger.LogInfo("Splash: resources loaded successfully.");

                if (_onComplete == null)
                {
                    if (_logger != null) _logger.LogError("Splash: _onComplete is NULL!");
                    StatusText.Text = "Error: onComplete delegate missing.";
                    await Task.Delay(2000);
                    Hide();
                    return;
                }

                if (_logger != null) _logger.LogInfo("Splash: invoking onComplete...");
                StatusText.Text = "Synchronizing...";

                try
                {
                    await _onComplete.Invoke();
                    if (_logger != null) _logger.LogInfo("Splash: onComplete completed successfully.");
                }
                catch (Exception ex)
                {
                    if (_logger != null) _logger.LogError("Splash: onComplete threw an exception", ex);
                    StatusText.Text = $"Error: {ex.Message}";
                    await Task.Delay(2000);
                    throw;
                }

                StatusText.Text = "Ready!";
                if (_logger != null) _logger.LogInfo("Splash: all done, hiding.");
                await Task.Delay(500);

                // Hide instead of Close – allows re‑show during shutdown
                var fadeOut = (Storyboard)FindResource("FadeOut");
                if (fadeOut != null)
                {
                    fadeOut.Completed += (s, _) =>
                    {
                        Hide(); 
                    };
                    fadeOut.Begin(MainBorder);
                }
                else
                {
                    Hide();
                }
            }
            catch (Exception ex)
            {
                _loadException = ex;
                if (_logger != null) _logger.LogError("Splash: initialization error", ex);
                else System.Diagnostics.Debug.WriteLine($"Splash error: {ex}");

                System.Windows.MessageBox.Show($"Initialization error: {ex.Message}\n\nCheck log for details.", "Startup Error", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Error);
                Close(); // On fatal error, we can close
            }
        }
    }
}