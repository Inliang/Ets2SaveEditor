using System;
using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;

namespace Ets2SaveEditor.Converters
{
    /// <summary>
    /// 将 0~1 的磨损值映射为绿(新)到红(报废)的画刷。
    /// </summary>
    public class WearToColorConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            double w = 0;
            if (value is double d) w = d;
            else if (value is int i) w = i;
            w = Math.Max(0, Math.Min(1, w));
            byte r = (byte)(w * 255);
            byte g = (byte)((1 - w) * 255);
            return new SolidColorBrush(Color.FromRgb(r, g, 0));
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
