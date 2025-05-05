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
    public partial class PaymentDialog : Window
    {
        private readonly decimal _totalAmount;
        private CultureInfo _culture = CultureInfo.CurrentCulture; // Используем CurrentCulture

        public string SelectedPaymentType { get; private set; } = "Cash"; // Default
        public decimal CashPaid { get; private set; } = 0m;
        public decimal CardPaid { get; private set; } = 0m;
        public decimal Change { get; private set; } = 0m;

        public PaymentDialog(decimal totalAmount)
        {
            InitializeComponent();
            _totalAmount = totalAmount;
            // Убедимся, что сумма не отрицательная
            if (_totalAmount < 0) _totalAmount = 0;
        }

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            TotalAmountText.Text = _totalAmount.ToString("C", _culture);
            CashReceivedTextBox.Text = _totalAmount.ToString("N2", _culture); // Use "N2" for numeric input formatting
            CashReceivedTextBox.Focus();
            CashReceivedTextBox.SelectAll();
            UpdatePaymentDetails();
        }

        private void PaymentType_Changed(object sender, RoutedEventArgs e)
        {
            // Проверка, загрузились ли элементы XAML
            if (CashInputPanel == null || ChangePanel == null || CardPaymentPanel == null || OkButton == null || CashRadioButton == null) return;

            string newPaymentType = "Cash"; // Default

            if (CashRadioButton.IsChecked == true)
            {
                newPaymentType = "Cash";
                CashInputPanel.Visibility = Visibility.Visible;
                ChangePanel.Visibility = Visibility.Visible;
                CardPaymentPanel.Visibility = Visibility.Collapsed;
                // Устанавливаем фокус только если окно уже загружено
                if (this.IsLoaded) { Dispatcher.BeginInvoke(new Action(() => CashReceivedTextBox.Focus()), System.Windows.Threading.DispatcherPriority.Input); }
            }
            else if (CardRadioButton.IsChecked == true)
            {
                newPaymentType = "Card";
                CashInputPanel.Visibility = Visibility.Collapsed;
                ChangePanel.Visibility = Visibility.Collapsed;
                CardPaymentPanel.Visibility = Visibility.Collapsed;
                if (this.IsLoaded) { Dispatcher.BeginInvoke(new Action(() => OkButton.Focus()), System.Windows.Threading.DispatcherPriority.Input); }
            }
            else if (MixedRadioButton.IsChecked == true)
            {
                newPaymentType = "Mixed";
                CashInputPanel.Visibility = Visibility.Visible;
                ChangePanel.Visibility = Visibility.Collapsed;
                CardPaymentPanel.Visibility = Visibility.Visible;
                if (this.IsLoaded) { Dispatcher.BeginInvoke(new Action(() => CashReceivedTextBox.Focus()), System.Windows.Threading.DispatcherPriority.Input); }
            }

            if (SelectedPaymentType != newPaymentType)
            {
                SelectedPaymentType = newPaymentType;
                // Пересчитываем детали только если тип изменился и окно загружено
                if (this.IsLoaded)
                {
                    UpdatePaymentDetails();
                    ClearError(); // Сбрасываем ошибку при смене типа
                }
            }
        }

        private void CashReceivedTextBox_PreviewTextInput(object sender, TextCompositionEventArgs e)
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

        private void CashReceivedTextBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            // Вызываем UpdatePaymentDetails только если окно загружено, чтобы избежать ошибок при инициализации
            if (this.IsLoaded)
            {
                UpdatePaymentDetails();
            }
        }

        private void UpdatePaymentDetails()
        {
            ClearError();
            decimal cashReceived = 0m;

            if (CashInputPanel.Visibility == Visibility.Visible)
            {
                if (decimal.TryParse(CashReceivedTextBox.Text, NumberStyles.Any, _culture, out decimal parsedCash))
                {
                    cashReceived = parsedCash;
                }
                else if (!string.IsNullOrWhiteSpace(CashReceivedTextBox.Text))
                {
                    ShowError("Некорректный формат суммы наличных.");
                    ChangeAmountText.Text = "---";
                    CardPaymentAmountText.Text = "---";
                    OkButton.IsEnabled = false; // Блокируем кнопку OK
                    return;
                }
            }

            // Сбрасываем значения перед расчетом
            CashPaid = 0m;
            CardPaid = 0m;
            Change = 0m;
            bool enableOkButton = true; // Флаг для кнопки OK

            switch (SelectedPaymentType)
            {
                case "Cash":
                    if (cashReceived >= _totalAmount)
                    {
                        Change = cashReceived - _totalAmount;
                        CashPaid = _totalAmount;
                    }
                    else
                    {
                        ShowError("Недостаточно наличных.");
                        CashPaid = cashReceived; // Сохраняем сколько внесено
                        enableOkButton = false; // Нельзя завершить оплату
                    }
                    ChangeAmountText.Text = Change.ToString("C", _culture);
                    break;

                case "Card":
                    CardPaid = _totalAmount;
                    break;

                case "Mixed":
                    if (cashReceived < 0)
                    {
                        ShowError("Сумма наличных не может быть отрицательной.");
                        enableOkButton = false;
                        CardPaymentAmountText.Text = "---";
                    }
                    else if (cashReceived >= _totalAmount)
                    {
                        CashPaid = _totalAmount; // Оплачено все наличными
                        Change = cashReceived - _totalAmount; // Будет сдача
                        ShowError("Наличных достаточно, оплата картой не требуется.");
                        // enableOkButton остается true, но логика ОК должна это учесть
                        CardPaymentAmountText.Text = 0m.ToString("C", _culture); // К оплате картой 0
                    }
                    else
                    {
                        CashPaid = cashReceived;
                        CardPaid = _totalAmount - cashReceived;
                        CardPaymentAmountText.Text = CardPaid.ToString("C", _culture);
                    }
                    break;
            }
            OkButton.IsEnabled = enableOkButton; // Управляем кнопкой OK
        }

        private void OkButton_Click(object sender, RoutedEventArgs e)
        {
            UpdatePaymentDetails(); // Финальный пересчет и проверка

            if (!OkButton.IsEnabled) // Если кнопка заблокирована проверками в UpdatePaymentDetails
            {
                // Ошибка уже должна быть показана
                if (CashInputPanel.IsVisible) CashReceivedTextBox.Focus();
                return;
            }

            // Особый случай: Смешанная оплата, но наличных хватило
            if (SelectedPaymentType == "Mixed" && CashPaid >= _totalAmount)
            {
                // Пользователь подтвердил оплату, хотя можно было бы оплатить только наличными.
                // Сохраняем как 'Mixed', CardPaid будет 0, будет рассчитана сдача.
                // (Альтернатива: принудительно сменить SelectedPaymentType на "Cash" здесь)
            }

            // Имитация пин-пада
            if (SelectedPaymentType == "Card" || (SelectedPaymentType == "Mixed" && CardPaid > 0))
            {
                MessageBoxResult pinpadResult = MessageBox.Show($"Имитация банковского терминала:\nК оплате картой: {CardPaid:C}\n\nОперация прошла успешно?",
                                                                "Банковский терминал", MessageBoxButton.YesNo, MessageBoxImage.Question);
                if (pinpadResult == MessageBoxResult.No)
                {
                    ShowError("Операция по карте отклонена.");
                    OkButton.IsEnabled = true; // Оставляем кнопку активной для повтора/смены типа
                    return;
                }
            }

            this.DialogResult = true; // Закрываем окно
        }

        private void CancelButton_Click(object sender, RoutedEventArgs e) => this.DialogResult = false;

        private void ShowError(string message) { PaymentErrorText.Text = message; PaymentErrorText.Visibility = Visibility.Visible; }
        private void ClearError() { PaymentErrorText.Text = string.Empty; PaymentErrorText.Visibility = Visibility.Collapsed; }

        protected override void OnKeyDown(KeyEventArgs e)
        {
            base.OnKeyDown(e);
            if (!e.Handled)
            {
                switch (e.Key)
                {
                    case Key.F7: CashRadioButton.IsChecked = true; e.Handled = true; break;
                    case Key.F8: CardRadioButton.IsChecked = true; e.Handled = true; break;
                    case Key.F9: MixedRadioButton.IsChecked = true; e.Handled = true; break;
                        // Обработка Enter уже делается через IsDefault на кнопке OK
                        // Обработка Esc уже делается через IsCancel на кнопке Cancel
                }
            }
        }
    }
}