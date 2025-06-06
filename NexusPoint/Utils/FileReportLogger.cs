using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.IO;

namespace NexusPoint.Utils
{
    public class FileReportLogger
    {
        // Файл будет создан в директории запуска программы (например, bin/Debug)
        private readonly string _logFilePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "reports_log.txt");

        public void AppendReportToFile(string reportContent)
        {
            try
            {
                StringBuilder sb = new StringBuilder();
                sb.AppendLine($"\n--- Отчет от {DateTime.Now:F} ---");
                sb.AppendLine(reportContent);
                sb.AppendLine("===================================================");

                // Используем синхронный метод AppendAllText
                File.AppendAllText(_logFilePath, sb.ToString());
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Ошибка записи отчета в файл: {ex.Message}");
            }
        }
    }
}
