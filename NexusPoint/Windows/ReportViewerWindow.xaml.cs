using Microsoft.Win32;
using NexusPoint.Utils;
using NPOI.XWPF.UserModel;
using System;
using System.IO;
using System.Windows;

namespace NexusPoint.Windows
{
    public partial class ReportViewerWindow : Window
    {
        private readonly string _reportTitle;
        private readonly string _reportContent;

        public ReportViewerWindow(string title, string content)
        {
            InitializeComponent();
            _reportTitle = title;
            _reportContent = content;

            this.Title = title;
            ReportTextBlock.Text = content;
        }

        private void PrintButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                PrinterService.Print(_reportTitle, _reportContent);
                MessageBox.Show("Отчет отправлен на печать.", "Печать", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка печати: {ex.Message}", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void SaveAsWordButton_Click(object sender, RoutedEventArgs e)
        {
            var saveFileDialog = new SaveFileDialog
            {
                Filter = "Word Document (*.docx)|*.docx",
                Title = "Сохранить отчет как...",
                FileName = $"{_reportTitle.Replace(":", "")} - {DateTime.Now:yyyy-MM-dd HH-mm}"
            };

            if (saveFileDialog.ShowDialog() == true)
            {
                try
                {
                    var document = new XWPFDocument();
                    string[] lines = _reportContent.Split(new[] { "\r\n", "\r", "\n" }, StringSplitOptions.None);
                    foreach (var line in lines)
                    {
                        var paragraph = document.CreateParagraph();
                        var run = paragraph.CreateRun();
                        run.SetText(line);
                        run.FontFamily = "Consolas";
                        run.FontSize = 10;
                    }
                    using (var fs = new FileStream(saveFileDialog.FileName, FileMode.Create, FileAccess.Write))
                    {
                        document.Write(fs);
                    }

                    MessageBox.Show($"Отчет успешно сохранен в:\n{saveFileDialog.FileName}", "Сохранение успешно", MessageBoxButton.OK, MessageBoxImage.Information);
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Не удалось сохранить файл: {ex.Message}", "Ошибка сохранения", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }

        private void CloseButton_Click(object sender, RoutedEventArgs e)
        {
            this.Close();
        }
    }
}