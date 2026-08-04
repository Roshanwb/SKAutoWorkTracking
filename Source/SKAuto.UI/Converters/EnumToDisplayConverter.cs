using System;
using System.Globalization;
using System.Windows.Data;
using SKAuto.Core.Enums;

namespace SKAuto.UI.Converters
{
    public class EnumToDisplayConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is OrderType orderType)
                return orderType.GetDisplayName();
            return value?.ToString() ?? string.Empty;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}