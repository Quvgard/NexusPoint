using NexusPoint.BusinessLogic;
using NexusPoint.Data.Repositories;
using NexusPoint.Models;
using NexusPoint.Utils;
using System;
using System.Collections.Generic;
using System.ComponentModel;
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
    public partial class LoginWindow : Window
    {
        public User AuthenticatedUser { get; private set; }

        // Используем AuthManager
        private readonly AuthManager _authManager;
        private bool _preventCancel = false; // Флаг для режима блокировки

        // Конструктор по умолчанию
        public LoginWindow()
        {
            InitializeComponent();
            // Создаем AuthManager (можно использовать DI в будущем)
            _authManager = new AuthManager(new UserRepository());

            Loaded += (sender, e) => UsernameTextBox.Focus();
            PasswordBox.KeyDown += PasswordBox_KeyDown;
        }

        // Конструктор для блокировки/презаполнения
        public LoginWindow(string defaultUsername, bool preventCancel = false) : this()
        {
            _preventCancel = preventCancel; // Сохраняем флаг

            if (!string.IsNullOrEmpty(defaultUsername))
            {
                UsernameTextBox.Text = defaultUsername;
                // Перенаправляем фокус с Username на Password
                Loaded -= (sender, e) => UsernameTextBox.Focus(); // Удаляем старый обработчик
                Loaded += (sender, e) => PasswordBox.Focus(); // Добавляем новый
            }
            if (_preventCancel)
            {
                // Блокируем кнопку Отмена И закрытие окна крестиком/Alt+F4 (см. OnClosing)
                CancelButton.IsEnabled = false;
            }
        }


        private async void LoginButton_Click(object sender, RoutedEventArgs e)
        {
            await AttemptLogin();
        }

        private async void PasswordBox_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
            {
                e.Handled = true;
                await AttemptLogin();
            }
        }

        private async Task AttemptLogin()
        {
            ClearError();
            string username = UsernameTextBox.Text.Trim();
            string password = PasswordBox.Password;

            if (string.IsNullOrWhiteSpace(username)) { ShowError("Пожалуйста, введите имя пользователя (ШК)."); UsernameTextBox.Focus(); return; }
            if (string.IsNullOrEmpty(password)) { ShowError("Пожалуйста, введите пароль."); PasswordBox.Focus(); return; }

            SetButtonsEnabled(false); // Блокируем кнопки

            // Вызываем AuthManager для аутентификации
            AuthenticationResult authResult = await _authManager.AuthenticateUserAsync(username, password);

            // Обрабатываем результат
            switch (authResult.Status)
            {
                case AuthResultStatus.Success:
                    AuthenticatedUser = authResult.AuthenticatedUser;
                    this.DialogResult = true; // Успех -> закрываем окно
                    break;
                case AuthResultStatus.UserNotFound:
                    ShowError(authResult.ErrorMessage);
                    UsernameTextBox.Focus(); UsernameTextBox.SelectAll();
                    PasswordBox.Clear();
                    break;
                case AuthResultStatus.InvalidPassword:
                    ShowError(authResult.ErrorMessage);
                    PasswordBox.Focus(); PasswordBox.Clear();
                    break;
                case AuthResultStatus.Error:
                    ShowError(authResult.ErrorMessage); // Показываем ошибку из AuthManager
                    PasswordBox.Clear();
                    break;
            }

            // Включаем кнопки обратно, если не было успешного выхода
            if (this.DialogResult != true)
            {
                SetButtonsEnabled(true);
            }
        }


        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            // Кнопка Отмена работает только если preventCancel = false
            if (!_preventCancel)
            {
                this.DialogResult = false;
            }
        }

        // Вспомогательные методы для UI (остаются без изменений)
        private void ShowError(string message) { ErrorMessageText.Text = message; ErrorMessageText.Visibility = Visibility.Visible; }
        private void ClearError() { ErrorMessageText.Text = string.Empty; ErrorMessageText.Visibility = Visibility.Collapsed; }
        private void SetButtonsEnabled(bool isEnabled)
        {
            LoginButton.IsEnabled = isEnabled;
            // Управляем кнопкой Cancel в зависимости от флага _preventCancel
            CancelButton.IsEnabled = isEnabled && !_preventCancel;
        }

        // Переопределяем закрытие окна для режима блокировки
        protected override void OnClosing(CancelEventArgs e)
        {
            base.OnClosing(e);

            // Запрещаем закрытие окна (крестиком, Alt+F4), если:
            // 1. Это режим блокировки (_preventCancel = true)
            // 2. И окно закрывается НЕ из-за установки DialogResult (т.е. не после успешного логина)
            if (_preventCancel && this.DialogResult == null)
            {
                e.Cancel = true; // Отменяем закрытие
                                 // Можно показать сообщение, но оно может мешать
                                 // MessageBox.Show("Для продолжения работы необходимо войти в систему.", "Вход обязателен", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }
    }
}