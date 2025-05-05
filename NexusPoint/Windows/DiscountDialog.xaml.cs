using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using System.Windows.Threading;

namespace NexusPoint.Windows
{
    public partial class DiscountDialog : Window
    {
        private readonly decimal _originalTotalAmount;
        private CultureInfo _culture = CultureInfo.CurrentCulture;

        // Результаты диалога
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
            UpdateCalculations(); // Инициализируем расчеты и суффикс
            // Фокус устанавливается на первый элемент с TabIndex=1 (PercentageRadioButton)
            PercentageRadioButton.Focus();
        }

        // Навигация стрелками по RadioButton
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

        // Смена типа скидки
        private void DiscountType_Changed(object sender, RoutedEventArgs e)
        {
            // Проверяем, загружены ли элементы
            if (PercentageRadioButton == null || AmountRadioButton == null || ValueSuffixText == null) return;

            IsPercentage = PercentageRadioButton.IsChecked == true;
            ValueSuffixText.Text = IsPercentage ? "%" : _culture.NumberFormat.CurrencySymbol;
            UpdateCalculations(); // Пересчитываем при смене типа
        }

        // Валидация ввода значения (только цифры и разделитель)
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

        // Изменение значения скидки
        private void DiscountValueTextBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            // Обновляем только если окно загружено
            if (this.IsLoaded)
            {
                UpdateCalculations();
            }
        }

        // Пересчет суммы скидки и итоговой суммы
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
            UpdateCalculations(); // Финальный пересчет и валидация

            // Проверяем ошибки валидации из UpdateCalculations
            if (ErrorText.Visibility == Visibility.Visible)
            {
                // Сообщение уже показано в ErrorText
                return;
            }
            if (DiscountValue <= 0) // Не применяем нулевую скидку
            {
                // Сообщение об этом не обязательно, т.к. кнопка ОК будет неактивна
                // MessageBox.Show("Введите положительное значение скидки.", "Ввод скидки", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            // Если CalculatedDiscountAmount равен 0 (например, при попытке применить скидку к нулевой сумме чека)
            if (CalculatedDiscountAmount <= 0 && _originalTotalAmount > 0)
            {
                // Это может произойти, если скидка очень мала и округлилась до нуля.
                // Можно либо разрешить закрытие, либо предупредить.
                // Пока разрешим.
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