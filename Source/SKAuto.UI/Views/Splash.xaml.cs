using System;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media.Animation;

namespace SKAuto.UI.Views
{
    public partial class Splash : Window
    {
        private readonly Action _loadResources;
        private readonly Action _onComplete;
        private Exception _loadException;

        public Splash(Action loadResources, Action onComplete)
        {
            InitializeComponent();
            _loadResources = loadResources;
            _onComplete = onComplete;
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
                await Task.Delay(100);

                // Run the actual resource loading in a background task
                await Task.Run(() =>
                {
                    _loadResources?.Invoke();
                });

                StatusText.Text = "Ready!";
                await Task.Delay(1000); // wait 1 sec after loading

                // Fade out and close
                var fadeOut = (Storyboard)FindResource("FadeOut");
                if (fadeOut != null)
                {
                    fadeOut.Completed += (s, _) =>
                    {
                        Close();
                        _onComplete?.Invoke();
                    };
                    fadeOut.Begin(MainBorder);
                }
                else
                {
                    Close();
                    _onComplete?.Invoke();
                }
            }
            catch (Exception ex)
            {
                _loadException = ex;
                // Show error and close without launching main window
                MessageBox.Show($"Initialization error: {ex.Message}", "Startup Error", MessageBoxButton.OK, MessageBoxImage.Error);
                Close();
                // Do not call _onComplete
            }
        }
    }
}