using System;
using System.IO;
using System.Text;

namespace NexusPoint.Utils
{
    public class FileReportLogger
    {
        private readonly string _logFilePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "reports_log.txt");

        public void AppendReportToFile(string reportContent)
        {
            try
            {
                StringBuilder sb = new StringBuilder();
                sb.AppendLine($"\n--- Отчет от {DateTime.Now:F} ---");
                sb.AppendLine(reportContent);
                sb.AppendLine("===================================================");
                File.AppendAllText(_logFilePath, sb.ToString());
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Ошибка записи отчета в файл: {ex.Message}");
            }
        }
    }
}
