using SKAuto.Core.Interfaces;
using System.Windows;
using System.Windows.Media.Animation;

namespace SKAuto.UI.Views
{
    public partial class Splash : Window
    {
        private readonly Action _loadResources;
        private readonly Action _onComplete;
        private readonly ILoggingService _logger;
        private Exception _loadException;

        public Splash(Action loadResources, Action onComplete, ILoggingService logger = null)
        {
            InitializeComponent();
            _loadResources = loadResources;
            _onComplete = onComplete;
            _logger = logger;
            Loaded += Splash_Loaded;
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

                // Run the actual resource loading in a background task
                await Task.Run(() =>
                {
                    _loadResources?.Invoke();
                });

                StatusText.Text = "Ready!";
                if (_logger != null) _logger.LogInfo("Splash: resources loaded successfully.");
                await Task.Delay(2000); // wait 1 sec after loading

                // Fade out and close
                var fadeOut = (Storyboard)FindResource("FadeOut");
                if (fadeOut != null)
                {
                    fadeOut.Completed += (s, _) =>
                    {
                        //Close();
                        Hide();
                        _onComplete?.Invoke();
                    };
                    fadeOut.Begin(MainBorder);
                }
                else
                {
                    //Close();
                    Hide();
                    _onComplete?.Invoke();
                }
            }
            catch (Exception ex)
            {
                _loadException = ex;
                if (_logger != null) _logger.LogError("Splash: initialization error", ex);
                else System.Diagnostics.Debug.WriteLine($"Splash error: {ex}");

                // Show error and close without launching main window
                System.Windows.MessageBox.Show($"Initialization error: {ex.Message}\n\nCheck log for details.", "Startup Error", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Error);
                Close();
                // Do not call _onComplete
            }
        }
    }
}