using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;

namespace NexusPoint.Utils
{
    public static class PrinterService
    {
        /// <summary>
        /// Имитирует печать переданного текстового содержимого.
        /// </summary>
        /// <param name="title">Заголовок для окна имитации.</param>
        /// <param name="content">Текст для "печати".</param>
        public static void Print(string title, string content)
        {
            // 1. Вывод в окно отладки Visual Studio
            Debug.WriteLine($"--- START PRINT: {title} ---");
            Debug.WriteLine(content);
            Debug.WriteLine($"--- END PRINT: {title} ---");

            // 2. Показ содержимого в MessageBox для наглядности
            // В реальном приложении здесь будет вызов драйвера принтера
            // или формирование ESC/POS команд.
            MessageBox.Show(content, title, MessageBoxButton.OK, MessageBoxImage.Information);

            // 3. Опционально: Запись в файл лога 
            try
            {
                string logFilePath = System.IO.Path.Combine(
                    AppDomain.CurrentDomain.BaseDirectory,
                    "receipts_log.txt"); // Файл в папке с EXE

                StringBuilder sb = new StringBuilder();
                sb.AppendLine($"--- {DateTime.Now:G} | PRINT: {title} ---");
                sb.AppendLine(content);
                sb.AppendLine("--- END ---");
                sb.AppendLine();

                System.IO.File.AppendAllText(logFilePath, sb.ToString());
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error writing to receipt log: {ex.Message}");
                // Не показываем MessageBox об ошибке лога, чтобы не мешать пользователю
            }
        }

        // Можно добавить другие методы, например, для открытия денежного ящика (если он подключен к принтеру)
        public static void OpenCashDrawer()
        {
            Debug.WriteLine("--- Open Cash Drawer ---");
            MessageBox.Show("Касса открыта", "Денежный ящик", MessageBoxButton.OK, MessageBoxImage.Information);
        }
    }
}