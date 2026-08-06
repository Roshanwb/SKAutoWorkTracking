using System.Globalization;
using System.Windows.Data;

namespace SKAuto.UI.Converters
{
    public class DateVisibilityConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is DateTime date && date != DateTime.MinValue)
                return date.ToString("dd/MM/yyyy");
            return "—"; // em dash for missing dates
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
            => throw new NotImplementedException();
    }
}