using System.Globalization;
using System.Windows.Data;

namespace Tagger
{
    public class WidthConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is double windowWidth &&
                parameter is string paramStr &&
                double.TryParse(paramStr, NumberStyles.Any, CultureInfo.InvariantCulture, out double percent))
            {
                return windowWidth * percent;
            }

            return 200.0;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
