using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace NexusPoint.Windows
{
    public partial class InputDialog : Window
    {
        public string InputText { get; private set; }

        private readonly bool _isPasswordMode;
        public InputDialog(string windowTitle, string prompt, string defaultValue = "", bool isPassword = false)
        {
            InitializeComponent();
            this.Title = windowTitle;
            PromptText.Text = prompt;
            _isPasswordMode = isPassword;

            if (_isPasswordMode)
            {
                InputTextBox.Visibility = Visibility.Collapsed;
                PasswordInputBox.Visibility = Visibility.Visible;
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
            if (_isPasswordMode)
            {
                Dispatcher.BeginInvoke(new Action(() => PasswordInputBox.Focus()), System.Windows.Threading.DispatcherPriority.Input);
            }
            else
            {
                Dispatcher.BeginInvoke(new Action(() =>
                {
                    InputTextBox.Focus();
                    InputTextBox.SelectAll();
                }), System.Windows.Threading.DispatcherPriority.Input);
            }
        }
        private void InputTextBox_KeyDown(object sender, KeyEventArgs e)
        {
            if (!_isPasswordMode && e.Key == Key.Enter)
            {
                MoveFocusToOkButton();
                e.Handled = true;
            }
        }

        private void PasswordInputBox_KeyDown(object sender, KeyEventArgs e)
        {
            if (_isPasswordMode && e.Key == Key.Enter)
            {
                MoveFocusToOkButton();
                e.Handled = true;
            }
        }
        private void MoveFocusToOkButton()
        {
            TraversalRequest request = new TraversalRequest(FocusNavigationDirection.Next);
            UIElement elementWithFocus = Keyboard.FocusedElement as UIElement;
            elementWithFocus?.MoveFocus(request);
        }
        private void OkButton_Click(object sender, RoutedEventArgs e)
        {
            InputText = _isPasswordMode ? PasswordInputBox.Password : InputTextBox.Text;
            this.DialogResult = true;
        }
        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            this.DialogResult = false;
        }

        private PasswordBox PasswordInputBox => (PasswordBox)this.FindName("InternalPasswordBox");
    }
}