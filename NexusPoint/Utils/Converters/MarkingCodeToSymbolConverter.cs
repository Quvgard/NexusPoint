using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Data;

namespace NexusPoint.Utils.Converters
{
    public class MarkingCodeToSymbolConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
        {
            // Если value (MarkingCode) не null и не пустая строка, возвращаем символ
            return !string.IsNullOrWhiteSpace(value as string) ? "[M]" : string.Empty;
        }

        public object ConvertBack(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
        {
            // Обратное преобразование не нужно
            throw new NotImplementedException();
        }
    }
}