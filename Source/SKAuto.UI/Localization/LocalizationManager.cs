using System.ComponentModel;
using System.Globalization;
using System.Resources;
using System.Runtime.CompilerServices;

namespace SKAuto.UI.Localization
{
    public class LocalizationManager : INotifyPropertyChanged
    {
        private static readonly Lazy<LocalizationManager> _instance = new(() => new LocalizationManager());
        public static LocalizationManager Instance => _instance.Value;

        private ResourceManager _resourceManager;
        private CultureInfo _currentCulture;

        private LocalizationManager()
        {
            _resourceManager = new ResourceManager("SKAuto.UI.Localization.Strings", typeof(LocalizationManager).Assembly);
            _currentCulture = CultureInfo.CurrentUICulture;
        }

        public CultureInfo CurrentCulture
        {
            get => _currentCulture;
            set
            {
                if (_currentCulture.Equals(value)) return;
                _currentCulture = value;
                Thread.CurrentThread.CurrentCulture = value;
                Thread.CurrentThread.CurrentUICulture = value;
                CultureInfo.DefaultThreadCurrentCulture = value;
                CultureInfo.DefaultThreadCurrentUICulture = value;
                OnPropertyChanged(nameof(CurrentCulture));
                OnPropertyChanged(string.Empty); // notify all bindings
            }
        }

        public string this[string key]
        {
            get
            {
                var value = _resourceManager.GetString(key, CurrentCulture);
                return value ?? key;
            }
        }

        public event PropertyChangedEventHandler PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string name = null) =>
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }
}