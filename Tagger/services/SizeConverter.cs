using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using System.Windows.Data;

namespace Tagger.viewmodel
{
    public class SizeConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is long bytes)
            {
                if (bytes < 1024) return $"{bytes} Б";

                var kb = bytes / 1024;
                if (kb < 1024) return $"{kb} КБ";

                var mb = kb / 1024;
                if (mb < 1024) return $"{mb} КБ";

                var gb = mb / 1024;
                if (gb < 1024) return $"{gb} КБ";
            }
            return "0 Б";
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
