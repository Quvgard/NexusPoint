using System;
using System.Globalization;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace NexusPoint.Windows
{
    public partial class DiscountDialog : Window
    {
        private readonly decimal _originalTotalAmount;
        private CultureInfo _culture = CultureInfo.CurrentCulture;
        public bool IsPercentage { get; private set; } = true;
        public decimal DiscountValue { get; private set; } = 0m;
        public decimal CalculatedDiscountAmount { get; private set; } = 0m;

        public DiscountDialog(decimal originalAmount)
        {
            InitializeComponent();
            _originalTotalAmount = Math.Max(0, originalAmount);
        }

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            OriginalAmountText.Text = _originalTotalAmount.ToString("C", _culture);
            UpdateCalculations();
            PercentageRadioButton.Focus();
        }
        private void RadioButton_KeyDown(object sender, KeyEventArgs e)
        {
            if (sender is RadioButton currentRadio)
            {
                RadioButton targetRadio = null;
                if (e.Key == Key.Left || e.Key == Key.Up)
                {
                    if (currentRadio == AmountRadioButton) targetRadio = PercentageRadioButton;
                }
                else if (e.Key == Key.Right || e.Key == Key.Down)
                {
                    if (currentRadio == PercentageRadioButton) targetRadio = AmountRadioButton;
                }

                if (targetRadio != null)
                {
                    targetRadio.IsChecked = true;
                    targetRadio.Focus();
                    e.Handled = true;
                }
            }
        }

        private void ValueTextBox_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
            {
                OkButton.Focus();
                e.Handled = true;
            }
        }
        private void DiscountType_Changed(object sender, RoutedEventArgs e)
        {
            if (PercentageRadioButton == null || AmountRadioButton == null || ValueSuffixText == null) return;

            IsPercentage = PercentageRadioButton.IsChecked == true;
            ValueSuffixText.Text = IsPercentage ? "%" : _culture.NumberFormat.CurrencySymbol;
            UpdateCalculations();
        }
        private void DiscountValueTextBox_PreviewTextInput(object sender, TextCompositionEventArgs e)
        {
            TextBox textBox = sender as TextBox;
            string currentText = textBox.Text.Insert(textBox.CaretIndex, e.Text);
            string decimalSeparator = _culture.NumberFormat.NumberDecimalSeparator;
            string pattern = $@"^\d*({Regex.Escape(decimalSeparator)}?\d*)?$";
            Regex regex = new Regex(pattern);
            if (!regex.IsMatch(currentText))
            {
                e.Handled = true;
            }
        }
        private void DiscountValueTextBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (this.IsLoaded)
            {
                UpdateCalculations();
            }
        }
        private void UpdateCalculations()
        {
            ClearError();
            OkButton.IsEnabled = false;
            CalculatedDiscountAmount = 0m;

            if (DiscountValueTextBox == null) return;

            if (!decimal.TryParse(DiscountValueTextBox.Text, NumberStyles.Any, _culture, out decimal value) || value < 0)
            {
                if (!string.IsNullOrEmpty(DiscountValueTextBox.Text))
                    ShowError("Некорректное значение.");
            }
            else
            {
                DiscountValue = value;
                if (value > 0)
                {
                    OkButton.IsEnabled = true;
                }


                if (IsPercentage)
                {
                    if (value > 100)
                    {
                        ShowError("Процент не может быть больше 100.");
                        CalculatedDiscountAmount = _originalTotalAmount;
                    }
                    else
                    {
                        CalculatedDiscountAmount = _originalTotalAmount * (value / 100m);
                    }
                }
                else
                {
                    if (value > _originalTotalAmount)
                    {
                        ShowError("Скидка не может быть больше суммы чека.");
                        CalculatedDiscountAmount = _originalTotalAmount;
                    }
                    else
                    {
                        CalculatedDiscountAmount = value;
                    }
                }
                CalculatedDiscountAmount = Math.Round(CalculatedDiscountAmount, 2);
            }

            if (DiscountAmountText != null) DiscountAmountText.Text = CalculatedDiscountAmount.ToString("C", _culture);
            if (FinalAmountText != null) FinalAmountText.Text = Math.Max(0, _originalTotalAmount - CalculatedDiscountAmount).ToString("C", _culture);
        }


        private void OkButton_Click(object sender, RoutedEventArgs e)
        {
            UpdateCalculations();
            if (ErrorText.Visibility == Visibility.Visible)
            {
                return;
            }
            if (DiscountValue <= 0)
            {
                return;
            }
            if (CalculatedDiscountAmount <= 0 && _originalTotalAmount > 0)
            {
            }
            else if (CalculatedDiscountAmount <= 0 && _originalTotalAmount <= 0)
            {
                MessageBox.Show("Сумма чека равна нулю, применение скидки не имеет смысла.", "Нулевой чек", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }


            this.DialogResult = true;
        }
        private void ShowError(string message) { if (ErrorText != null) { ErrorText.Text = message; ErrorText.Visibility = Visibility.Visible; } }
        private void ClearError() { if (ErrorText != null) { ErrorText.Text = string.Empty; ErrorText.Visibility = Visibility.Collapsed; } }
    }
}