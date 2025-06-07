using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace NexusPoint.Utils.Converters
{
    [ValueConversion(typeof(object), typeof(Visibility))]
    public class NotNullToVisibilityConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            bool isNull = value == null;
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
            throw new NotImplementedException();
        }
    }
}