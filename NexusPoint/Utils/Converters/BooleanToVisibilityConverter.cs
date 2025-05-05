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
            else if (value is bool?) // Также обрабатываем Nullable<bool>
            {
                bool? nullableBool = (bool?)value;
                boolValue = nullableBool.HasValue && nullableBool.Value;
            }

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

            if (invert)
            {
                boolValue = !boolValue;
            }

            // Определение видимости для False (по умолчанию Collapsed)
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
            // Обычно ConvertBack не нужен для этого конвертера
            if (value is Visibility visibility)
            {
                bool baseValue = visibility == Visibility.Visible;

                // Обработка параметра инвертирования при обратном преобразовании
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
            return DependencyProperty.UnsetValue; // Или false по умолчанию
        }
    }
}