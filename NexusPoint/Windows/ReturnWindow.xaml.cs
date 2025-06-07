using NexusPoint.BusinessLogic;
using NexusPoint.Data.Repositories;
using NexusPoint.Models;
using System;
using System.Globalization;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace NexusPoint.Windows
{
    public partial class ReturnWindow : Window
    {
        private readonly User CurrentUser;
        private readonly Shift CurrentShift;
        private readonly ReturnManager _returnManager;

        public ReturnWindow(User user, Shift shift)
        {
            InitializeComponent();
            CurrentUser = user ?? throw new ArgumentNullException(nameof(user));
            CurrentShift = shift;
            var checkRepository = new CheckRepository();
            var productRepository = new ProductRepository();
            var stockItemRepository = new StockItemRepository();
            _returnManager = new ReturnManager(checkRepository, productRepository, stockItemRepository);
            this.DataContext = _returnManager;
        }

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            if (CurrentShift == null)
            {
                ShowError("Ошибка: Не удалось определить текущую открытую смену. Возврат невозможен.");
                FindCheckButton.IsEnabled = false;
                CheckNumberTextBox.IsEnabled = false;
                ShiftNumberTextBox.IsEnabled = false;
                return;
            }
            ShiftNumberTextBox.Text = CurrentShift.ShiftNumber.ToString();
            CheckNumberTextBox.Focus();
        }

        private async void SearchTextBox_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
            {
                await FindCheckAsync();
                e.Handled = true;
            }
        }

        private async void FindCheckButton_Click(object sender, RoutedEventArgs e)
        {
            await FindCheckAsync();
        }

        private async Task FindCheckAsync()
        {
            ClearError();
            if (!int.TryParse(CheckNumberTextBox.Text, out int checkNumber) || checkNumber <= 0)
            { ShowError("Введите корректный номер чека."); return; }
            if (!int.TryParse(ShiftNumberTextBox.Text, out int shiftNumber) || shiftNumber <= 0)
            { ShowError("Введите корректный номер смены."); return; }

            FindCheckButton.IsEnabled = false;
            SearchIndicator.Visibility = Visibility.Visible;
            StatusText.Text = "Поиск чека...";

            bool found = await _returnManager.FindOriginalCheckAsync(checkNumber, shiftNumber);

            SearchIndicator.Visibility = Visibility.Collapsed;
            StatusText.Text = "";
            FindCheckButton.IsEnabled = true;


            if (!found && _returnManager.OriginalCheck == null)
            {
                if (string.IsNullOrEmpty(StatusText.Text))
                {
                    ShowError($"Чек продажи №{checkNumber} в смене №{shiftNumber} не найден или является чеком возврата.");
                }
            }
            else if (found)
            {
                UpdatePaymentTypeText();
            }
            CheckNumberTextBox.Focus();
            CheckNumberTextBox.SelectAll();
        }
        private void UpdatePaymentTypeText()
        {
            if (_returnManager.OriginalCheck != null)
            {
                string paymentTypeDisplay = _returnManager.OriginalCheck.PaymentType;
                switch (_returnManager.OriginalCheck.PaymentType?.ToLower())
                {
                    case "cash": paymentTypeDisplay = "Наличные"; break;
                    case "card": paymentTypeDisplay = "Карта"; break;
                    case "mixed": paymentTypeDisplay = "Смешанная"; break;
                }
                OriginalPaymentTypeText.Text = paymentTypeDisplay;
            }
            else
            {
                OriginalPaymentTypeText.Text = "";
            }
        }
        private void QuantityTextBox_PreviewTextInput(object sender, TextCompositionEventArgs e)
        {
            TextBox textBox = sender as TextBox;
            string currentText = textBox.Text.Insert(textBox.CaretIndex, e.Text);
            string decimalSeparator = CultureInfo.CurrentCulture.NumberFormat.NumberDecimalSeparator;
            string pattern = $@"^\d*({Regex.Escape(decimalSeparator)}?\d*)?$";
            Regex regex = new Regex(pattern);
            if (!regex.IsMatch(currentText)) e.Handled = true;
        }
        private void OriginalCheckListView_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter && OriginalCheckListView.SelectedItem is ReturnItemView selectedItem)
            {
                var listViewItem = OriginalCheckListView.ItemContainerGenerator.ContainerFromItem(selectedItem) as ListViewItem;
                if (listViewItem != null)
                {
                    var quantityTextBox = FindVisualChild<TextBox>(listViewItem);
                    if (quantityTextBox != null && quantityTextBox.IsEnabled)
                    {
                        quantityTextBox.Focus();
                        quantityTextBox.SelectAll();
                        e.Handled = true;
                    }
                }
            }
        }
        private static T FindVisualChild<T>(DependencyObject parent) where T : DependencyObject
        {
            if (parent == null) return null;
            for (int i = 0; i < VisualTreeHelper.GetChildrenCount(parent); i++)
            {
                DependencyObject child = VisualTreeHelper.GetChild(parent, i);
                if (child != null && child is T)
                {
                    return (T)child;
                }
                else
                {
                    T childOfChild = FindVisualChild<T>(child);
                    if (childOfChild != null)
                        return childOfChild;
                }
            }
            return null;
        }
        private void ReturnAllButton_Click(object sender, RoutedEventArgs e)
        {
            _returnManager.SetReturnQuantityForAll(true);
        }
        private void ClearSelectionButton_Click(object sender, RoutedEventArgs e)
        {
            _returnManager.SetReturnQuantityForAll(false);
        }
        private async void ProcessReturnButton_Click(object sender, RoutedEventArgs e)
        {
            ProcessReturnButton.IsEnabled = false;
            FindCheckButton.IsEnabled = false;
            StatusText.Text = "Обработка возврата...";

            bool success = await _returnManager.ProcessReturnAsync(CurrentShift, CurrentUser);

            StatusText.Text = "";
            FindCheckButton.IsEnabled = true;

            if (success)
            {
                MessageBox.Show("Возврат успешно оформлен.", "Возврат", MessageBoxButton.OK, MessageBoxImage.Information);
                this.DialogResult = true;
            }
            else
            {
                ProcessReturnButton.IsEnabled = _returnManager.CanProcessReturn;
            }
        }
        private void ShowError(string message, bool isInfo = false)
        {
            StatusText.Text = message;
            StatusText.Foreground = isInfo ? Brushes.Blue : Brushes.Red;
        }
        private void ClearError() { StatusText.Text = string.Empty; }

    }
}
