using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Data;
using System.Windows;

namespace NexusPoint.Utils.Converters
{
    [ValueConversion(typeof(object), typeof(Visibility))]
    public class NotNullToVisibilityConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            bool isNull = value == null;

            // Обработка параметра для инвертирования
            bool invert = false;
            if (parameter is string strParam)
            {
                string lowerParam = strParam.ToLowerInvariant();
                if (lowerParam == "invert" || lowerParam == "reverse" || lowerParam == "negate")
                {
                    invert = true;
                }
            }

            bool shouldBeVisible = invert ? isNull : !isNull;

            return shouldBeVisible ? Visibility.Visible : Visibility.Collapsed;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            // Обратное преобразование не имеет смысла для этого конвертера
            throw new NotImplementedException();
        }
    }
}