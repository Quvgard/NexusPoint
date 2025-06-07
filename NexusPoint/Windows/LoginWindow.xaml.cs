using NexusPoint.BusinessLogic;
using NexusPoint.Data.Repositories;
using NexusPoint.Models;
using System.ComponentModel;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace NexusPoint.Windows
{
    public partial class LoginWindow : Window
    {
        public User AuthenticatedUser { get; private set; }
        private readonly AuthManager _authManager;
        private bool _preventCancel = false;
        public LoginWindow()
        {
            InitializeComponent();
            _authManager = new AuthManager(new UserRepository());

            Loaded += (sender, e) => UsernameTextBox.Focus();
            PasswordBox.KeyDown += PasswordBox_KeyDown;
        }
        public LoginWindow(string defaultUsername, bool preventCancel = false) : this()
        {
            _preventCancel = preventCancel;

            if (!string.IsNullOrEmpty(defaultUsername))
            {
                UsernameTextBox.Text = defaultUsername;
                Loaded -= (sender, e) => UsernameTextBox.Focus();
                Loaded += (sender, e) => PasswordBox.Focus();
            }
            if (_preventCancel)
            {
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

            SetButtonsEnabled(false);
            AuthenticationResult authResult = await _authManager.AuthenticateUserAsync(username, password);
            switch (authResult.Status)
            {
                case AuthResultStatus.Success:
                    AuthenticatedUser = authResult.AuthenticatedUser;
                    this.DialogResult = true;
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
                    ShowError(authResult.ErrorMessage);
                    PasswordBox.Clear();
                    break;
            }
            if (this.DialogResult != true)
            {
                SetButtonsEnabled(true);
            }
        }


        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            if (!_preventCancel)
            {
                this.DialogResult = false;
            }
        }
        private void ShowError(string message) { ErrorMessageText.Text = message; ErrorMessageText.Visibility = Visibility.Visible; }
        private void ClearError() { ErrorMessageText.Text = string.Empty; ErrorMessageText.Visibility = Visibility.Collapsed; }
        private void SetButtonsEnabled(bool isEnabled)
        {
            LoginButton.IsEnabled = isEnabled;
            CancelButton.IsEnabled = isEnabled && !_preventCancel;
        }
        protected override void OnClosing(CancelEventArgs e)
        {
            base.OnClosing(e);
            if (_preventCancel && this.DialogResult == null)
            {
                e.Cancel = true;
            }
        }
    }
}