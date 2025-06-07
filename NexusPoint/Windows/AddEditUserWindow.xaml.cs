using NexusPoint.BusinessLogic;
using NexusPoint.Models;
using System;
using System.Windows;
using System.Windows.Controls;

namespace NexusPoint.Windows
{
    public partial class AddEditUserWindow : Window
    {
        private readonly UserManager _userManager;
        private readonly User _originalUser;
        private bool IsEditMode => _originalUser != null;
        public AddEditUserWindow(UserManager userManager)
        {
            InitializeComponent();
            _userManager = userManager ?? throw new ArgumentNullException(nameof(userManager));
            _originalUser = null;
            this.Title = "Добавление пользователя";
        }
        public AddEditUserWindow(UserManager userManager, User userToEdit) : this(userManager)
        {
            _originalUser = userToEdit ?? throw new ArgumentNullException(nameof(userToEdit));
            this.Title = "Редактирование пользователя";
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
            PasswordInfoText.Visibility = Visibility.Visible;
            PasswordLabel.Content = "_Пароль:";
            ConfirmPasswordLabel.Content = "_Подтвердите пароль:";
        }

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            UsernameTextBox.Focus();
        }
        private void SaveButton_Click(object sender, RoutedEventArgs e)
        {
            ClearError();
            string username = UsernameTextBox.Text.Trim();
            string fullName = FullNameTextBox.Text.Trim();
            string password = PasswordBox.Password;
            string confirmPassword = ConfirmPasswordBox.Password;
            string selectedRole = (RoleComboBox.SelectedItem as ComboBoxItem)?.Content.ToString();
            if (string.IsNullOrWhiteSpace(username)) { ShowError("Логин (ШК) не может быть пустым."); UsernameTextBox.Focus(); return; }
            if (string.IsNullOrWhiteSpace(fullName)) { ShowError("ФИО не может быть пустым."); FullNameTextBox.Focus(); return; }
            if (string.IsNullOrEmpty(selectedRole)) { ShowError("Выберите роль пользователя."); RoleComboBox.Focus(); return; }
            bool updatePassword = false;
            if (!string.IsNullOrEmpty(password))
            {
                if (password != confirmPassword)
                {
                    ShowError("Пароли не совпадают.");
                    ConfirmPasswordBox.Focus(); ConfirmPasswordBox.SelectAll();
                    return;
                }
                updatePassword = true;
            }
            else if (!IsEditMode)
            {
                ShowError("Пароль обязателен при создании нового пользователя.");
                PasswordBox.Focus();
                return;
            }
            User userToSave;
            if (IsEditMode) { userToSave = _originalUser; }
            else { userToSave = new User(); }

            userToSave.Username = username;
            userToSave.FullName = fullName;
            userToSave.Role = selectedRole;
            bool success;
            if (IsEditMode)
            {
                success = _userManager.UpdateUser(userToSave, updatePassword ? password : null);
            }
            else
            {
                success = _userManager.AddUser(userToSave, password);
            }
            if (success)
            {
                this.DialogResult = true;
            }
        }
        private void ShowError(string message) { ErrorText.Text = message; ErrorText.Visibility = Visibility.Visible; }
        private void ClearError() { ErrorText.Text = string.Empty; ErrorText.Visibility = Visibility.Collapsed; }
    }
}