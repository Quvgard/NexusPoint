using System;
using System.Globalization;
using System.Windows.Data;

namespace NexusPoint.Utils.Converters
{
    public class BooleanToSaleReturnConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is bool isReturn)
            {
                return isReturn ? "Возврат" : "Продажа";
            }
            return string.Empty;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}