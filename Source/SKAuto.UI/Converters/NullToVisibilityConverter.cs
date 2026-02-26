using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace SKAuto.UI.Converters
{
    public class NullToVisibilityConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            // If parameter is "inverse", show when NULL (for Add New text)
            bool inverse = parameter?.ToString()?.ToLower() == "inverse";

            if (inverse)
                return value == null ? Visibility.Visible : Visibility.Collapsed;
            else
                return value != null ? Visibility.Visible : Visibility.Collapsed;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}