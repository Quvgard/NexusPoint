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
    /// Логика взаимодействия для PaymentDialog.xaml
    /// </summary>
    public partial class PaymentDialog : Window
    {
        private readonly decimal _totalAmount; // Сумма к оплате

        // Публичные свойства для получения результата
        public string SelectedPaymentType { get; private set; } = "Cash"; // По умолчанию
        public decimal CashPaid { get; private set; } = 0m;
        public decimal CardPaid { get; private set; } = 0m;
        public decimal Change { get; private set; } = 0m;


        public PaymentDialog(decimal totalAmount)
        {
            InitializeComponent();
            _totalAmount = totalAmount;
        }

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            CultureInfo culture = CultureInfo.CurrentCulture; // Или GetCultureInfo("ru-RU")
            TotalAmountText.Text = _totalAmount.ToString("C", culture);

            // Изначально выбран Cash, предустанавливаем сумму
            CashReceivedTextBox.Text = _totalAmount.ToString("N2", culture); // Предзаполняем поле ввода суммой чека
            CashReceivedTextBox.Focus();
            CashReceivedTextBox.SelectAll();
            UpdatePaymentDetails(); // Рассчитать сдачу
        }

        // Обработка смены типа оплаты
        private void PaymentType_Changed(object sender, RoutedEventArgs e)
        {
            if (CashInputPanel == null || ChangePanel == null || CardPaymentPanel == null || OkButton == null || CashRadioButton == null)
            {
                return;
            }

            if (CashRadioButton.IsChecked == true)
            {
                SelectedPaymentType = "Cash";
                CashInputPanel.Visibility = Visibility.Visible;
                ChangePanel.Visibility = Visibility.Visible;
                CardPaymentPanel.Visibility = Visibility.Collapsed;

                if (this.IsLoaded) CashReceivedTextBox.Focus();
            }
            else if (CardRadioButton.IsChecked == true)
            {
                SelectedPaymentType = "Card";
                CashInputPanel.Visibility = Visibility.Collapsed; // Скрываем ввод наличных
                ChangePanel.Visibility = Visibility.Collapsed;
                CardPaymentPanel.Visibility = Visibility.Collapsed;
                if (this.IsLoaded) OkButton.Focus();
            }
            else if (MixedRadioButton.IsChecked == true)
            {
                SelectedPaymentType = "Mixed";
                CashInputPanel.Visibility = Visibility.Visible;
                ChangePanel.Visibility = Visibility.Collapsed;
                CardPaymentPanel.Visibility = Visibility.Visible;
                if (this.IsLoaded) CashReceivedTextBox.Focus();
            }
            if (this.IsLoaded)
            {
                UpdatePaymentDetails();
                ClearError();
            }
        }

        // Валидация ввода в поле Наличные (только цифры и один разделитель)
        private void CashReceivedTextBox_PreviewTextInput(object sender, TextCompositionEventArgs e)
        {
            TextBox textBox = sender as TextBox;
            string currentText = textBox.Text.Insert(textBox.CaretIndex, e.Text);
            string decimalSeparator = CultureInfo.CurrentCulture.NumberFormat.NumberDecimalSeparator;

            // Паттерн: начало строки (^), ноль или больше цифр (\d*),
            // необязательно десятичный разделитель (\.{decimalSeparator}?),
            // ноль или больше цифр (\d*), конец строки ($)
            // Используем Regex.Escape для экранирования разделителя, если это точка
            string pattern = $@"^\d*({Regex.Escape(decimalSeparator)}?\d*)?$";

            Regex regex = new Regex(pattern);

            // Если новый текст не соответствует паттерну, отменяем ввод
            if (!regex.IsMatch(currentText))
            {
                e.Handled = true;
            }
        }

        // Обработка изменения текста в поле Наличные
        private void CashReceivedTextBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            UpdatePaymentDetails();
        }

        // Пересчет сдачи / доплаты картой
        private void UpdatePaymentDetails()
        {
            ClearError(); // Сбросить ошибки при любом изменении

            CultureInfo culture = CultureInfo.CurrentCulture;
            decimal cashReceived = 0m;

            // Пытаемся получить сумму из TextBox
            if (CashInputPanel.Visibility == Visibility.Visible &&
               decimal.TryParse(CashReceivedTextBox.Text, NumberStyles.Any, culture, out decimal parsedCash))
            {
                cashReceived = parsedCash;
            }
            else if (CashInputPanel.Visibility == Visibility.Visible && !string.IsNullOrWhiteSpace(CashReceivedTextBox.Text))
            {
                // Если поле видимо, но парсинг не удался (например, вводится некорректный символ)
                ShowError("Некорректный формат суммы наличных.");
                ChangeAmountText.Text = "---";
                CardPaymentAmountText.Text = "---";
                return; // Прерываем расчет
            }


            Change = 0m;
            CardPaid = 0m; // Сбрасываем перед расчетом

            if (SelectedPaymentType == "Cash")
            {
                if (cashReceived >= _totalAmount)
                {
                    Change = cashReceived - _totalAmount;
                    CashPaid = _totalAmount; // Оплачено наличными вся сумма
                }
                else
                {
                    Change = 0m; // Недостаточно средств
                    CashPaid = cashReceived; // Оплачено только то, что внесено
                    ShowError("Недостаточно наличных.");
                }
                ChangeAmountText.Text = Change.ToString("C", culture);
            }
            else if (SelectedPaymentType == "Card")
            {
                CashPaid = 0m;
                CardPaid = _totalAmount; // Вся сумма картой
                Change = 0m;
            }
            else if (SelectedPaymentType == "Mixed")
            {
                if (cashReceived >= _totalAmount)
                {
                    // Если наличных хватает или больше, это по сути оплата наличными
                    CashPaid = _totalAmount;
                    CardPaid = 0m;
                    Change = cashReceived - _totalAmount; // Возвращаем сдачу наличными
                    // Можно предложить переключиться на тип "Наличные", но пока оставим так
                    ShowError("Наличных достаточно, оплата картой не требуется. Будет выдана сдача.");
                    CardPaymentAmountText.Text = CardPaid.ToString("C", culture);
                }
                else if (cashReceived < 0) // Отрицательную сумму не принимаем
                {
                    ShowError("Сумма наличных не может быть отрицательной.");
                    CardPaymentAmountText.Text = "---";
                    return;
                }
                else
                {
                    // Наличных недостаточно, остаток картой
                    CashPaid = cashReceived;
                    CardPaid = _totalAmount - cashReceived;
                    Change = 0m; // Сдачи нет
                    CardPaymentAmountText.Text = CardPaid.ToString("C", culture);
                }
            }
        }


        // Нажатие OK
        private void OkButton_Click(object sender, RoutedEventArgs e)
        {
            UpdatePaymentDetails(); // Пересчитать на всякий случай

            // Проверка валидности перед закрытием
            if (SelectedPaymentType == "Cash" && CashPaid < _totalAmount)
            {
                MessageBox.Show("Недостаточно наличных для завершения оплаты.", "Ошибка оплаты", MessageBoxButton.OK, MessageBoxImage.Warning);
                CashReceivedTextBox.Focus();
                return;
            }
            if (SelectedPaymentType == "Mixed" && (CashPaid + CardPaid) < _totalAmount)
            {
                MessageBox.Show("Недостаточно средств для завершения смешанной оплаты.", "Ошибка оплаты", MessageBoxButton.OK, MessageBoxImage.Warning);
                CashReceivedTextBox.Focus();
                return;
            }
            // Дополнительная проверка для смешанной оплаты, где наличных больше чем надо
            if (SelectedPaymentType == "Mixed" && CashPaid >= _totalAmount)
            {
                // Если пользователь все же нажал OK при смешанной оплате, но внес достаточно наличных
                // Мы рассчитали CardPaid = 0 и Change > 0
                // Сохраняем как смешанную оплату с нулевой суммой по карте и сдачей
                // или можно принудительно сменить тип на Cash? Решаем здесь.
                // Пока оставляем как есть: сохранится как Mixed с CardPaid=0 и будет сдача.
            }


            // Если оплата картой (Card или Mixed с CardPaid > 0)
            if (SelectedPaymentType == "Card" || (SelectedPaymentType == "Mixed" && CardPaid > 0))
            {
                // --- ИМИТАЦИЯ РАБОТЫ С ПИН-ПАДОМ ---
                // В реальном приложении здесь был бы вызов SDK банковского терминала
                MessageBoxResult pinpadResult = MessageBox.Show($"Имитация банковского терминала:\nК оплате картой: {CardPaid:C}\n\nОперация прошла успешно?",
                                                                "Банковский терминал", MessageBoxButton.YesNo, MessageBoxImage.Question);

                if (pinpadResult == MessageBoxResult.No)
                {
                    ShowError("Операция по карте отклонена банком.");
                    return; // Не закрываем окно, даем возможность изменить способ оплаты
                }
                // Если Yes - продолжаем
            }

            // Все проверки пройдены, тип оплаты и суммы рассчитаны в UpdatePaymentDetails()
            this.DialogResult = true; // Устанавливаем результат и закрываем
        }

        // Нажатие Отмена
        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            this.DialogResult = false;
        }

        // Показ ошибки
        private void ShowError(string message)
        {
            PaymentErrorText.Text = message;
            PaymentErrorText.Visibility = Visibility.Visible;
        }

        // Скрытие ошибки
        private void ClearError()
        {
            PaymentErrorText.Text = string.Empty;
            PaymentErrorText.Visibility = Visibility.Collapsed;
        }

        // Обработка горячих клавиш F7, F8, F9
        protected override void OnKeyDown(KeyEventArgs e)
        {
            base.OnKeyDown(e);
            if (!e.Handled)
            {
                switch (e.Key)
                {
                    case Key.F7:
                        CashRadioButton.IsChecked = true;
                        e.Handled = true;
                        break;
                    case Key.F8:
                        CardRadioButton.IsChecked = true;
                        e.Handled = true;
                        break;
                    case Key.F9:
                        MixedRadioButton.IsChecked = true;
                        e.Handled = true;
                        break;
                }
            }
        }
    }
}