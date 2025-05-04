using NexusPoint.Data.Repositories;
using NexusPoint.Models;
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
    /// Логика взаимодействия для AddEditUserWindow.xaml
    /// </summary>
    public partial class AddEditUserWindow : Window
    {
        private readonly UserRepository _userRepository;
        private readonly User _originalUser; // null если режим добавления
        private bool IsEditMode => _originalUser != null;

        // Конструктор для добавления
        public AddEditUserWindow()
        {
            InitializeComponent();
            _userRepository = new UserRepository();
            this.Title = "Добавление пользователя";
        }

        // Конструктор для редактирования
        public AddEditUserWindow(User userToEdit) : this()
        {
            _originalUser = userToEdit;
            this.Title = "Редактирование пользователя";

            // Заполняем поля
            UsernameTextBox.Text = _originalUser.Username;
            FullNameTextBox.Text = _originalUser.FullName;

            // Выбираем роль в ComboBox
            foreach (ComboBoxItem item in RoleComboBox.Items)
            {
                if (item.Content.ToString() == _originalUser.Role)
                {
                    RoleComboBox.SelectedItem = item;
                    break;
                }
            }

            // Показываем подсказку про пароль
            PasswordInfoText.Visibility = Visibility.Visible;
            // Метки обязательности пароля убираем (или меняем текст)
            PasswordLabel.Content = "_Пароль:";
            ConfirmPasswordLabel.Content = "_Подтвердите пароль:";
        }

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            UsernameTextBox.Focus();
        }

        // Кнопка Сохранить
        private void SaveButton_Click(object sender, RoutedEventArgs e)
        {
            ClearError();

            // 1. Получаем данные из полей
            string username = UsernameTextBox.Text.Trim();
            string fullName = FullNameTextBox.Text.Trim();
            string password = PasswordBox.Password; // Пароль получаем как есть
            string confirmPassword = ConfirmPasswordBox.Password;
            string selectedRole = (RoleComboBox.SelectedItem as ComboBoxItem)?.Content.ToString();

            // 2. Валидация
            if (string.IsNullOrWhiteSpace(username))
            {
                ShowError("Логин (ШК) не может быть пустым.");
                UsernameTextBox.Focus();
                return;
            }
            if (string.IsNullOrWhiteSpace(fullName))
            {
                ShowError("ФИО не может быть пустым.");
                FullNameTextBox.Focus();
                return;
            }
            if (string.IsNullOrEmpty(selectedRole))
            {
                ShowError("Выберите роль пользователя.");
                RoleComboBox.Focus();
                return;
            }

            // Проверка уникальности логина (кроме себя в режиме ред.)
            var existingUser = _userRepository.GetUserByUsername(username);
            if (existingUser != null && (!IsEditMode || existingUser.UserId != _originalUser.UserId))
            {
                ShowError($"Пользователь с логином '{username}' уже существует.");
                UsernameTextBox.Focus();
                UsernameTextBox.SelectAll();
                return;
            }

            // Валидация пароля
            bool updatePassword = false;
            if (!string.IsNullOrEmpty(password)) // Если поле пароля не пустое, значит пытаемся установить/сменить
            {
                if (password.Length < 4) // Пример минимальной длины пароля
                {
                    ShowError("Пароль должен содержать не менее 4 символов.");
                    PasswordBox.Focus();
                    return;
                }
                if (password != confirmPassword)
                {
                    ShowError("Пароли не совпадают.");
                    ConfirmPasswordBox.Focus();
                    ConfirmPasswordBox.SelectAll();
                    return;
                }
                updatePassword = true; // Пароли совпали, будем обновлять
            }
            else if (!IsEditMode) // Если это режим ДОБАВЛЕНИЯ и пароль пустой
            {
                ShowError("Пароль обязателен при создании нового пользователя.");
                PasswordBox.Focus();
                return;
            }

            // 3. Создание/обновление объекта User
            User userToSave;
            if (IsEditMode)
            {
                userToSave = _originalUser;
            }
            else
            {
                userToSave = new User();
            }

            userToSave.Username = username;
            userToSave.FullName = fullName;
            userToSave.Role = selectedRole;
            // HashedPassword будет установлен при вызове AddUser или UpdateUserPassword

            // 4. Сохранение в БД
            try
            {
                bool success;
                if (IsEditMode)
                {
                    // Сначала обновляем основные данные
                    success = _userRepository.UpdateUser(userToSave);
                    if (success && updatePassword) // Если нужно обновить пароль
                    {
                        success = _userRepository.UpdateUserPassword(userToSave.UserId, password);
                    }
                }
                else // Режим добавления
                {
                    int newId = _userRepository.AddUser(userToSave, password); // Сразу передаем пароль
                    success = newId > 0;
                }

                if (success)
                {
                    this.DialogResult = true; // Успех
                }
                else
                {
                    ShowError("Не удалось сохранить данные пользователя.");
                }
            }
            catch (Exception ex)
            {
                ShowError($"Ошибка сохранения: {ex.Message}");
                System.Diagnostics.Debug.WriteLine($"Save user error: {ex}");
            }
        }

        // Показ/Скрытие ошибки
        private void ShowError(string message)
        {
            ErrorText.Text = message;
            ErrorText.Visibility = Visibility.Visible;
        }
        private void ClearError()
        {
            ErrorText.Text = string.Empty;
            ErrorText.Visibility = Visibility.Collapsed;
        }
    }
}