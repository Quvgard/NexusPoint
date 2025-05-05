using NexusPoint.BusinessLogic;
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
        // Заменяем репозиторий на менеджер
        private readonly UserManager _userManager;
        private readonly User _originalUser;
        private bool IsEditMode => _originalUser != null;

        // Конструктор для добавления (принимает менеджер)
        public AddEditUserWindow(UserManager userManager)
        {
            InitializeComponent();
            _userManager = userManager ?? throw new ArgumentNullException(nameof(userManager));
            _originalUser = null;
            this.Title = "Добавление пользователя";
        }

        // Конструктор для редактирования (принимает менеджер и пользователя)
        public AddEditUserWindow(UserManager userManager, User userToEdit) : this(userManager) // Вызов основного конструктора
        {
            _originalUser = userToEdit ?? throw new ArgumentNullException(nameof(userToEdit));
            this.Title = "Редактирование пользователя";

            // Заполнение полей
            UsernameTextBox.Text = _originalUser.Username;
            FullNameTextBox.Text = _originalUser.FullName;
            foreach (ComboBoxItem item in RoleComboBox.Items)
            {
                if (item.Content.ToString() == _originalUser.Role)
                {
                    RoleComboBox.SelectedItem = item;
                    break;
                }
            }

            // UI для пароля в режиме редактирования
            PasswordInfoText.Visibility = Visibility.Visible;
            PasswordLabel.Content = "_Пароль:"; // Убираем *
            ConfirmPasswordLabel.Content = "_Подтвердите пароль:"; // Убираем *
        }

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            UsernameTextBox.Focus();
        }

        // Кнопка Сохранить (обновлено)
        private void SaveButton_Click(object sender, RoutedEventArgs e)
        {
            ClearError();

            // 1. Получаем данные из полей
            string username = UsernameTextBox.Text.Trim();
            string fullName = FullNameTextBox.Text.Trim();
            string password = PasswordBox.Password;
            string confirmPassword = ConfirmPasswordBox.Password;
            string selectedRole = (RoleComboBox.SelectedItem as ComboBoxItem)?.Content.ToString();

            // 2. Базовая валидация (остается здесь)
            if (string.IsNullOrWhiteSpace(username)) { ShowError("Логин (ШК) не может быть пустым."); UsernameTextBox.Focus(); return; }
            if (string.IsNullOrWhiteSpace(fullName)) { ShowError("ФИО не может быть пустым."); FullNameTextBox.Focus(); return; }
            if (string.IsNullOrEmpty(selectedRole)) { ShowError("Выберите роль пользователя."); RoleComboBox.Focus(); return; }

            // Валидация паролей (только совпадение, остальное - в менеджере)
            bool updatePassword = false;
            if (!string.IsNullOrEmpty(password)) // Если пароль введен (попытка установить/сменить)
            {
                if (password != confirmPassword)
                {
                    ShowError("Пароли не совпадают.");
                    ConfirmPasswordBox.Focus(); ConfirmPasswordBox.SelectAll();
                    return;
                }
                updatePassword = true; // Пароли совпали, передадим его менеджеру
            }
            else if (!IsEditMode) // Обязателен при создании
            {
                // Эта проверка также есть в менеджере, но оставим и здесь для UI
                ShowError("Пароль обязателен при создании нового пользователя.");
                PasswordBox.Focus();
                return;
            }

            // 3. Создание/обновление объекта User
            User userToSave;
            if (IsEditMode) { userToSave = _originalUser; } // Берем существующий
            else { userToSave = new User(); } // Создаем новый

            userToSave.Username = username;
            userToSave.FullName = fullName;
            userToSave.Role = selectedRole;
            // HashedPassword будет установлен менеджером

            // 4. Сохранение через UserManager
            bool success;
            if (IsEditMode)
            {
                // Передаем пользователя и опционально новый пароль
                success = _userManager.UpdateUser(userToSave, updatePassword ? password : null);
            }
            else // Режим добавления
            {
                // Передаем пользователя и обязательный пароль
                success = _userManager.AddUser(userToSave, password);
            }

            // 5. Закрытие окна при успехе
            if (success)
            {
                this.DialogResult = true;
            }
            // Сообщения об ошибках (включая валидацию длины пароля и уникальность) будут показаны менеджером
        }

        // Показ/Скрытие ошибки (остаются)
        private void ShowError(string message) { ErrorText.Text = message; ErrorText.Visibility = Visibility.Visible; }
        private void ClearError() { ErrorText.Text = string.Empty; ErrorText.Visibility = Visibility.Collapsed; }
    }
}