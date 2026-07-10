using System.Windows;
using System.Windows.Data;
using System.Windows.Markup;

namespace SKAuto.UI.Localization
{
    public class TranslateExtension : MarkupExtension
    {
        private readonly string _key;

        public TranslateExtension(string key)
        {
            _key = key;
        }

        public override object ProvideValue(IServiceProvider serviceProvider)
        {
            // Return a binding to the LocalizationManager's indexer
            var binding = new System.Windows.Data.Binding
            {
                Source = LocalizationManager.Instance,
                Path = new PropertyPath($"Item[{_key}]"), // indexer
                Mode = BindingMode.OneWay,
                UpdateSourceTrigger = UpdateSourceTrigger.PropertyChanged
            };
            return binding.ProvideValue(serviceProvider);
        }
    }
}