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
        public string InputText { get; private set; }

        private bool _isPasswordMode = false; // Флаг режима пароля

        // Обновленный конструктор
        public InputDialog(string windowTitle, string prompt, string defaultValue = "", bool isPassword = false)
        {
            InitializeComponent();
            this.Title = windowTitle;
            PromptText.Text = prompt;
            _isPasswordMode = isPassword;

            if (_isPasswordMode)
            {
                // Если режим пароля, скрываем TextBox и показываем PasswordBox
                InputTextBox.Visibility = Visibility.Collapsed;
                // Создаем PasswordBox динамически или имеем его в XAML и просто показываем
                PasswordInputBox.Visibility = Visibility.Visible;
                PasswordInputBox.Password = defaultValue; // Устанавливаем значение по умолчанию (если нужно)
            }
            else
            {
                // Обычный режим, показываем TextBox
                PasswordInputBox.Visibility = Visibility.Collapsed;
                InputTextBox.Visibility = Visibility.Visible;
                InputTextBox.Text = defaultValue;
            }
        }

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            if (_isPasswordMode)
            {
                PasswordInputBox.Focus();
                // Выделение всего текста в PasswordBox не работает стандартно
            }
            else
            {
                InputTextBox.Focus();
                InputTextBox.SelectAll();
            }
        }

        // Нажатие Enter в TextBox
        private void InputTextBox_KeyDown(object sender, KeyEventArgs e)
        {
            if (!_isPasswordMode && e.Key == Key.Enter)
            {
                OkButton.Focus();
            }
        }

        // Нажатие Enter в PasswordBox
        private void PasswordInputBox_KeyDown(object sender, KeyEventArgs e)
        {
            if (_isPasswordMode && e.Key == Key.Enter)
            {
                OkButton.Focus();
            }
        }

        // Нажатие OK
        private void OkButton_Click(object sender, RoutedEventArgs e)
        {
            // Получаем текст из активного поля
            InputText = _isPasswordMode ? PasswordInputBox.Password : InputTextBox.Text;
            this.DialogResult = true;
        }

        // Нажатие Отмена
        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            this.DialogResult = false;
        }

        // --- Элемент PasswordBox нужно добавить в XAML ---
        // Поле для хранения ссылки на PasswordBox из XAML
        private PasswordBox PasswordInputBox => (PasswordBox)this.FindName("InternalPasswordBox");
    }
}