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
        // Свойство для получения введенного текста или пароля
        public string InputText { get; private set; }

        private readonly bool _isPasswordMode;

        /// <summary>
        /// Создает диалоговое окно для ввода текста или пароля.
        /// </summary>
        /// <param name="windowTitle">Заголовок окна.</param>
        /// <param name="prompt">Текст подсказки для пользователя.</param>
        /// <param name="defaultValue">Значение по умолчанию для поля ввода.</param>
        /// <param name="isPassword">True, если нужно использовать поле для ввода пароля (PasswordBox).</param>
        public InputDialog(string windowTitle, string prompt, string defaultValue = "", bool isPassword = false)
        {
            InitializeComponent();
            this.Title = windowTitle;
            PromptText.Text = prompt;
            _isPasswordMode = isPassword;

            if (_isPasswordMode)
            {
                InputTextBox.Visibility = Visibility.Collapsed;
                PasswordInputBox.Visibility = Visibility.Visible; // Используем имя из XAML
                PasswordInputBox.Password = defaultValue;
            }
            else
            {
                PasswordInputBox.Visibility = Visibility.Collapsed;
                InputTextBox.Visibility = Visibility.Visible;
                InputTextBox.Text = defaultValue;
            }
        }

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            // Устанавливаем фокус и выделяем текст (если возможно)
            if (_isPasswordMode)
            {
                // Установка фокуса через Dispatcher для надежности после загрузки
                Dispatcher.BeginInvoke(new Action(() => PasswordInputBox.Focus()), System.Windows.Threading.DispatcherPriority.Input);
            }
            else
            {
                Dispatcher.BeginInvoke(new Action(() => {
                    InputTextBox.Focus();
                    InputTextBox.SelectAll();
                }), System.Windows.Threading.DispatcherPriority.Input);
            }
        }

        // Нажатие Enter в полях ввода переводит фокус на OK
        private void InputTextBox_KeyDown(object sender, KeyEventArgs e)
        {
            if (!_isPasswordMode && e.Key == Key.Enter)
            {
                MoveFocusToOkButton();
                e.Handled = true; // Поглощаем Enter
            }
        }

        private void PasswordInputBox_KeyDown(object sender, KeyEventArgs e)
        {
            if (_isPasswordMode && e.Key == Key.Enter)
            {
                MoveFocusToOkButton();
                e.Handled = true; // Поглощаем Enter
            }
        }

        // Перевод фокуса на кнопку OK (для срабатывания IsDefault)
        private void MoveFocusToOkButton()
        {
            // Создаем запрос на перемещение фокуса
            TraversalRequest request = new TraversalRequest(FocusNavigationDirection.Next);
            // Ищем элемент с фокусом
            UIElement elementWithFocus = Keyboard.FocusedElement as UIElement;
            // Перемещаем фокус, если нашли элемент
            elementWithFocus?.MoveFocus(request);
            // Альтернативно, просто установить фокус:
            // OkButton.Focus();
        }


        // Нажатие OK
        private void OkButton_Click(object sender, RoutedEventArgs e)
        {
            InputText = _isPasswordMode ? PasswordInputBox.Password : InputTextBox.Text;
            this.DialogResult = true; // Закрываем окно с результатом true
        }

        // Нажатие Отмена (срабатывает IsCancel="True" в XAML)
        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            this.DialogResult = false; // Закрываем окно с результатом false
        }

        private PasswordBox PasswordInputBox => (PasswordBox)this.FindName("InternalPasswordBox");
    }
}