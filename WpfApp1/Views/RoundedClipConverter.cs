using System.Globalization;
using System.Windows;
using System.Windows.Data;
using System.Windows.Media;

namespace WpfApp1.Views
{
    public sealed class RoundedClipConverter : IMultiValueConverter
    {
        public object Convert(object?[] values, Type targetType, object? parameter, CultureInfo culture)
        {
            if (values.Length < 2 || values[0] is not double w || values[1] is not double h || w <= 0 || h <= 0)
                return Geometry.Empty;

            var geometry = new RectangleGeometry(new Rect(0, 0, w, h), 14, 14);
            geometry.Freeze();
            return geometry;
        }

        public object?[] ConvertBack(object? value, Type[] targetTypes, object? parameter, CultureInfo culture)
            => throw new NotSupportedException();
    }
}
