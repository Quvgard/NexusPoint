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

namespace NexusPoint.Windows
{
    /// <summary>
    /// Логика взаимодействия для InputDialog.xaml
    /// </summary>
    public partial class InputDialog : Window
    {
        // Публичное свойство для доступа к введенному тексту
        public string InputText { get; private set; }

        // Конструктор, принимающий заголовок окна, текст запроса и значение по умолчанию
        public InputDialog(string windowTitle, string prompt, string defaultValue = "")
        {
            InitializeComponent();
            this.Title = windowTitle; // Устанавливаем заголовок окна
            PromptText.Text = prompt; // Устанавливаем текст запроса
            InputTextBox.Text = defaultValue; // Устанавливаем значение по умолчанию
        }

        // Загрузка окна: установить фокус и выделить текст
        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            InputTextBox.Focus();
            InputTextBox.SelectAll(); // Выделяем весь текст для удобства замены
        }

        // Нажатие Enter в TextBox = нажатие OK
        private void InputTextBox_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
            {
                // Не вызываем OkButton_Click напрямую, чтобы обработчик IsDefault сработал корректно
                // Просто имитируем завершение редактирования
                OkButton.Focus(); // Переводим фокус для срабатывания IsDefault
            }
        }

        // Нажатие кнопки OK
        private void OkButton_Click(object sender, RoutedEventArgs e)
        {
            InputText = InputTextBox.Text; // Сохраняем введенный текст
            this.DialogResult = true;      // Устанавливаем результат и закрываем окно
        }

        // Нажатие кнопки Отмена
        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            this.DialogResult = false;     // Устанавливаем результат и закрываем окно
        }
    }
}