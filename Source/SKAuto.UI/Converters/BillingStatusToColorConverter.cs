using System;
using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;
using SKAuto.Core.Enums;
using Color = System.Windows.Media.Color;

namespace SKAuto.UI.Converters
{
    public class BillingStatusToColorConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is BillingStatus status)
            {
                return status switch
                {
                    BillingStatus.Done => new SolidColorBrush(Color.FromRgb(46, 204, 113)),   // #2ECC71
                    BillingStatus.ToDo => new SolidColorBrush(Color.FromRgb(243, 156, 18)),   // #F39C12
                    BillingStatus.Pending => new SolidColorBrush(Color.FromRgb(231, 76, 60)),  // #E74C3C
                    _ => new SolidColorBrush(Colors.Gray)
                };
            }
            return new SolidColorBrush(Colors.Gray);
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}