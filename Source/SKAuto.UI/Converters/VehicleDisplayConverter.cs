using SKAuto.Core.Entities;
using System.Globalization;
using System.Windows.Data;

namespace SKAuto.UI.Converters
{
    public class VehicleDisplayConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is Vehicle vehicle)
            {
                var parts = new System.Collections.Generic.List<string>();
                if (!string.IsNullOrWhiteSpace(vehicle.Make))
                    parts.Add(vehicle.Make);
                if (!string.IsNullOrWhiteSpace(vehicle.Model))
                    parts.Add(vehicle.Model);
                if (!string.IsNullOrWhiteSpace(vehicle.ChassisNumber))
                    parts.Add($"({vehicle.ChassisNumber})");
                return parts.Count > 0 ? string.Join(" ", parts) : vehicle.ChassisNumber;
            }
            return "None";
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
            => throw new NotImplementedException();
    }
}