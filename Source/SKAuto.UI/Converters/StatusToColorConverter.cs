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
                    WorkStatus.Planned => Brushes.Blue,
                    WorkStatus.InProgress => Brushes.Orange,
                    WorkStatus.Blocked => Brushes.Red,
                    WorkStatus.Done => Brushes.Green,
                    _ => Brushes.Gray
                };
            }
            return Brushes.Gray;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
            => throw new NotImplementedException();
    }
}