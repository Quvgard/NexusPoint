using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Controls;
using System.Windows.Data;

namespace NexusPoint.Utils.Converters
{
    public class IndexConverter : IValueConverter
    {
        public object Convert(object value, Type TargetType, object parameter, System.Globalization.CultureInfo culture)
        {
            try
            {
                // Получаем ListViewItem
                ListViewItem item = (ListViewItem)value;
                // Получаем родительский ListView
                ListView listView = ItemsControl.ItemsControlFromItemContainer(item) as ListView;
                // Находим индекс этого элемента в коллекции ListView
                int index = listView.ItemContainerGenerator.IndexFromContainer(item);
                return (index + 1).ToString(); // +1, т.к. индексы с 0
            }
            catch
            {
                return string.Empty; // В случае ошибки
            }
        }

        public object ConvertBack(object value, Type TargetType, object parameter, System.Globalization.CultureInfo culture)
        {
            // Обратное преобразование не нужно
            throw new NotImplementedException();
        }
    }
}