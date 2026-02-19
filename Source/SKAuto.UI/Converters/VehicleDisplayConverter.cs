using System;
using System.Globalization;
using System.Windows.Data;
using SKAuto.Core.Entities;

namespace SKAuto.UI.Converters
{
    public class VehicleDisplayConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is Vehicle vehicle)
                return $"{vehicle.Make} {vehicle.Model} ({vehicle.ChassisNumber})".Trim();
            return "None";
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
            => throw new NotImplementedException();
    }
}