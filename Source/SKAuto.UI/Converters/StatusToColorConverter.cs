using System;
using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;
using SKAuto.Core.Enums;

namespace SKAuto.UI.Converters
{
    public class StatusToColorConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is WorkStatus status)
            {
                return status switch
                {
                    WorkStatus.Planned => new SolidColorBrush(Colors.LightGray),
                    WorkStatus.InProgress => new SolidColorBrush(Colors.Orange),
                    WorkStatus.Blocked => new SolidColorBrush(Colors.Red),
                    WorkStatus.Done => new SolidColorBrush(Colors.Green),
                    _ => new SolidColorBrush(Colors.LightGray)
                };
            }
            return new SolidColorBrush(Colors.LightGray);
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}