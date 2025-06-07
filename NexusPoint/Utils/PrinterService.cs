using System;
using System.Diagnostics;
using System.Text;
using System.Windows;

namespace NexusPoint.Utils
{
    public static class PrinterService
    {
        public static void Print(string title, string content)
        {
            Debug.WriteLine($"--- START PRINT: {title} ---");
            Debug.WriteLine(content);
            Debug.WriteLine($"--- END PRINT: {title} ---");
            MessageBox.Show(content, title, MessageBoxButton.OK, MessageBoxImage.Information);
            try
            {
                string logFilePath = System.IO.Path.Combine(
                    AppDomain.CurrentDomain.BaseDirectory,
                    "receipts_log.txt");

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
            }
        }
        public static void OpenCashDrawer()
        {
            Debug.WriteLine("--- Open Cash Drawer ---");
            MessageBox.Show("Касса открыта", "Денежный ящик", MessageBoxButton.OK, MessageBoxImage.Information);
        }
    }
}