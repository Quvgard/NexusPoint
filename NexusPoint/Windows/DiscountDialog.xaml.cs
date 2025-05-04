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

namespace NexusPoint.Windows
{
    /// <summary>
    /// Логика взаимодействия для DiscountDialog.xaml
    /// </summary>
    public partial class DiscountDialog : Window
    {
        private readonly decimal _originalTotalAmount;

        // Результаты диалога
        public bool IsPercentage { get; private set; } = true; // По умолчанию процент
        public decimal DiscountValue { get; private set; } = 0m; // Введенное значение
        public decimal CalculatedDiscountAmount { get; private set; } = 0m; // Рассчитанная сумма скидки

        public DiscountDialog(decimal originalAmount)
        {
            InitializeComponent();
            _originalTotalAmount = originalAmount;
        }

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            OriginalAmountText.Text = _originalTotalAmount.ToString("C");
            DiscountValueTextBox.Focus();
            UpdateCalculations(); // Первичный расчет (с 0)
        }

        // Смена типа скидки
        private void DiscountType_Changed(object sender, RoutedEventArgs e)
        {
            if (PercentageRadioButton == null || AmountRadioButton == null) return; // Защита от null при загрузке

            IsPercentage = PercentageRadioButton.IsChecked == true;
            ValueSuffixText.Text = IsPercentage ? "%" : CultureInfo.CurrentCulture.NumberFormat.CurrencySymbol; // Показываем % или знак валюты
            UpdateCalculations();
        }

        // Валидация ввода значения (цифры, разделитель)
        private void DiscountValueTextBox_PreviewTextInput(object sender, TextCompositionEventArgs e)
        {
            TextBox textBox = sender as TextBox;
            string currentText = textBox.Text.Insert(textBox.CaretIndex, e.Text);
            string decimalSeparator = CultureInfo.CurrentCulture.NumberFormat.NumberDecimalSeparator;
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
            UpdateCalculations();
        }

        // Пересчет суммы скидки и итоговой суммы
        private void UpdateCalculations()
        {
            ClearError();
            if (!decimal.TryParse(DiscountValueTextBox.Text, NumberStyles.Any, CultureInfo.CurrentCulture, out decimal value) || value < 0)
            {
                if (!string.IsNullOrEmpty(DiscountValueTextBox.Text)) // Показываем ошибку только если что-то введено
                    ShowError("Некорректное значение.");
                CalculatedDiscountAmount = 0m;
            }
            else
            {
                DiscountValue = value; // Сохраняем введенное валидное значение
                if (IsPercentage)
                {
                    if (value > 100) // Ограничение процента
                    {
                        ShowError("Процент не может быть больше 100.");
                        CalculatedDiscountAmount = _originalTotalAmount; // Считаем как 100%
                    }
                    else
                    {
                        CalculatedDiscountAmount = _originalTotalAmount * (value / 100m);
                    }
                }
                else // Сумма
                {
                    if (value > _originalTotalAmount)
                    {
                        ShowError("Скидка не может быть больше суммы чека.");
                        CalculatedDiscountAmount = _originalTotalAmount; // Считаем как 100%
                    }
                    else
                    {
                        CalculatedDiscountAmount = value;
                    }
                }
                // Округляем до копеек
                CalculatedDiscountAmount = Math.Round(CalculatedDiscountAmount, 2);
            }


            // Отображаем результаты
            DiscountAmountText.Text = CalculatedDiscountAmount.ToString("C");
            FinalAmountText.Text = (_originalTotalAmount - CalculatedDiscountAmount).ToString("C");
        }


        private void OkButton_Click(object sender, RoutedEventArgs e)
        {
            UpdateCalculations(); // Финальный пересчет
            if (ErrorText.Visibility == Visibility.Visible) // Не закрываем, если есть ошибка валидации
            {
                MessageBox.Show(ErrorText.Text, "Ошибка ввода", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }
            if (DiscountValue <= 0) // Не применяем нулевую или отрицательную скидку
            {
                MessageBox.Show("Введите положительное значение скидки.", "Ввод скидки", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            this.DialogResult = true; // Устанавливаем результат
        }

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