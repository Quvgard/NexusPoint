using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace NexusPoint.Utils.Converters
{
    [ValueConversion(typeof(bool), typeof(Visibility))]
    public class BooleanToVisibilityConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            bool boolValue = false;
            if (value is bool b)
            {
                boolValue = b;
            }
            else if (value is bool?)
            {
                bool? nullableBool = (bool?)value;
                boolValue = nullableBool.HasValue && nullableBool.Value;
            }
            bool invert = false;
            if (parameter is string strParam)
            {
                string lowerParam = strParam.ToLowerInvariant();
                if (lowerParam == "invert" || lowerParam == "reverse" || lowerParam == "negate")
                {
                    invert = true;
                }
            }

            if (invert)
            {
                boolValue = !boolValue;
            }
            Visibility falseVisibility = Visibility.Collapsed;
            if (parameter is string strParamVisibility)
            {
                if (strParamVisibility.Equals("Hidden", StringComparison.OrdinalIgnoreCase))
                {
                    falseVisibility = Visibility.Hidden;
                }
            }

            return boolValue ? Visibility.Visible : falseVisibility;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is Visibility visibility)
            {
                bool baseValue = visibility == Visibility.Visible;
                bool invert = false;
                if (parameter is string strParam)
                {
                    string lowerParam = strParam.ToLowerInvariant();
                    if (lowerParam == "invert" || lowerParam == "reverse" || lowerParam == "negate")
                    {
                        invert = true;
                    }
                }

                return invert ? !baseValue : baseValue;
            }
            return DependencyProperty.UnsetValue;
        }
    }
}