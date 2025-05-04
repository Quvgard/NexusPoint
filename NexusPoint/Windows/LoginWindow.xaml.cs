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
    /// <summary>
    /// Логика взаимодействия для LoginWindow.xaml
    /// </summary>
    public partial class LoginWindow : Window
    {
        // Свойство для хранения успешно аутентифицированного пользователя
        public User AuthenticatedUser { get; private set; }

        private readonly UserRepository _userRepository; // Репозиторий для работы с пользователями

        public LoginWindow()
        {
            InitializeComponent();
            _userRepository = new UserRepository(); // Создаем экземпляр репозитория

            // Устанавливаем фокус на поле ввода имени пользователя при загрузке
            Loaded += (sender, e) => UsernameTextBox.Focus();

            // Опционально: Обработка Enter в PasswordBox для вызова логина
            PasswordBox.KeyDown += PasswordBox_KeyDown;
        }

        // НОВЫЙ КОНСТРУКТОР для блокировки
        public LoginWindow(string defaultUsername, bool preventCancel = false) : this() // Добавляем флаг preventCancel
        {
            if (!string.IsNullOrEmpty(defaultUsername))
            {
                UsernameTextBox.Text = defaultUsername;
                Loaded -= (sender, e) => UsernameTextBox.Focus();
                Loaded += (sender, e) => PasswordBox.Focus();
            }
            if (preventCancel) // Если отмена запрещена
            {
                CancelButton.IsEnabled = false; // Делаем кнопку Отмена неактивной
            }
        }


        // Обработчик нажатия кнопки "Войти"
        private async void LoginButton_Click(object sender, RoutedEventArgs e)
        {
            await AttemptLogin();
        }

        // Опционально: Обработчик нажатия Enter в поле пароля
        private async void PasswordBox_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
            {
                // Предотвращаем дальнейшую обработку Enter, чтобы не было "бип" звука
                e.Handled = true;
                await AttemptLogin();
            }
        }

        // Основная логика попытки входа
        private async Task AttemptLogin() // Сделаем асинхронным на всякий случай (хотя SQLite быстрый)
        {
            // Скрываем предыдущие ошибки
            ClearError();

            string username = UsernameTextBox.Text.Trim();
            string password = PasswordBox.Password; // Получаем пароль из PasswordBox

            // Простая валидация ввода
            if (string.IsNullOrWhiteSpace(username))
            {
                ShowError("Пожалуйста, введите имя пользователя (ШК).");
                UsernameTextBox.Focus();
                return;
            }
            if (string.IsNullOrEmpty(password)) // PasswordBox не может быть null, но может быть пустым
            {
                ShowError("Пожалуйста, введите пароль.");
                PasswordBox.Focus();
                return;
            }

            // Блокируем кнопки на время проверки
            SetButtonsEnabled(false);

            try
            {
                // Ищем пользователя в базе данных
                // В реальном приложении здесь может быть асинхронный вызов
                User user = _userRepository.GetUserByUsername(username);

                if (user == null)
                {
                    ShowError("Пользователь с таким именем не найден.");
                    UsernameTextBox.Focus(); // Возвращаем фокус на имя
                    PasswordBox.Clear();    // Очищаем пароль
                }
                // Проверяем пароль с использованием нашего хешера
                else if (PasswordHasher.VerifyPassword(password, user.HashedPassword))
                {
                    // Успешный вход!
                    AuthenticatedUser = user; // Сохраняем пользователя
                    this.DialogResult = true; // Устанавливаем результат диалога
                    // Окно закроется автоматически
                }
                else
                {
                    // Неверный пароль
                    ShowError("Неверный пароль.");
                    PasswordBox.Clear();    // Очищаем пароль
                    PasswordBox.Focus();    // Фокус на пароль для повторного ввода
                }
            }
            catch (Exception ex)
            {
                // Обработка общих ошибок (например, ошибка подключения к БД)
                System.Diagnostics.Debug.WriteLine($"Login error: {ex}");
                ShowError($"Произошла ошибка при входе: {ex.Message}");
                PasswordBox.Clear();
            }
            finally
            {
                // Включаем кнопки обратно в любом случае
                SetButtonsEnabled(true);
            }
        }


        // Обработчик нажатия кнопки "Отмена"
        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            this.DialogResult = false; // Устанавливаем результат и закрываем окно
        }

        // Вспомогательный метод для отображения ошибок
        private void ShowError(string message)
        {
            ErrorMessageText.Text = message;
            ErrorMessageText.Visibility = Visibility.Visible;
        }

        // Вспомогательный метод для скрытия ошибок
        private void ClearError()
        {
            ErrorMessageText.Text = string.Empty;
            ErrorMessageText.Visibility = Visibility.Collapsed;
        }

        // Вспомогательный метод для блокировки/разблокировки кнопок
        private void SetButtonsEnabled(bool isEnabled)
        {
            LoginButton.IsEnabled = isEnabled;
            CancelButton.IsEnabled = isEnabled;
            // Можно добавить индикатор загрузки (ProgressBar) и управлять его видимостью здесь же
        }

        // Переопределяем метод закрытия окна
        protected override void OnClosing(CancelEventArgs e)
        {
            base.OnClosing(e);

            // Разрешаем закрытие ТОЛЬКО если DialogResult был установлен (т.е. успешный логин или программное закрытие)
            // ИЛИ если пользователь нажал кнопку "Отмена" (DialogResult = false)
            // Мы НЕ хотим блокировать кнопку "Отмена"
            if (this.DialogResult == null)
            {
                // Если DialogResult еще не установлен (например, закрытие через Alt+F4 или крестик)
                // И если окно вызвано для РАЗБЛОКИРОВКИ (мы можем передать флаг или проверить Owner)
                if (this.Owner is CashierWindow) // Проверяем, что окно вызвано из CashierWindow (для разблокировки)
                {
                    // ЗАПРЕЩАЕМ закрытие окна
                    e.Cancel = true;
                    MessageBox.Show("Для продолжения работы необходимо войти в систему.", "Вход обязателен", MessageBoxButton.OK, MessageBoxImage.Warning);
                }
                // Если окно вызвано не для разблокировки (например, первичный вход из MainWindow),
                // то можно разрешить закрытие (e.Cancel остается false).
            }
            // Если DialogResult == true (успешный логин) или DialogResult == false (нажата Отмена), окно закроется само.
        }
    }
}