using Microsoft.Win32;
using NexusPoint.Utils;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using NPOI.XWPF.UserModel;
using System.IO;

namespace NexusPoint.Windows
{
    /// <summary>
    /// Логика взаимодействия для ReportViewerWindow.xaml
    /// </summary>
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
                    // 1. Создаем новый пустой .docx документ
                    var document = new XWPFDocument();

                    // 2. Разбиваем весь текст отчета на отдельные строки
                    string[] lines = _reportContent.Split(new[] { "\r\n", "\r", "\n" }, StringSplitOptions.None);

                    // 3. Создаем абзац для каждой строки
                    foreach (var line in lines)
                    {
                        var paragraph = document.CreateParagraph();
                        var run = paragraph.CreateRun();
                        run.SetText(line);
                        // Для сохранения моноширинного вида, как в отчете
                        run.FontFamily = "Consolas";
                        run.FontSize = 10;
                    }

                    // 4. Сохраняем документ в файл
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