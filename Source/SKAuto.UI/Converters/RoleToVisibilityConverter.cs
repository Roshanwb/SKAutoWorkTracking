using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;
using SKAuto.Core.Enums;

namespace SKAuto.UI.Converters
{
    public class RoleToVisibilityConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is UserRole role && parameter is string requiredRole)
            {
                if (Enum.TryParse<UserRole>(requiredRole, out var required))
                {
                    return role == required ? Visibility.Visible : Visibility.Collapsed;
                }
            }
            return Visibility.Collapsed;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}