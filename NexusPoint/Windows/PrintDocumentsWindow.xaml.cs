using NexusPoint.BusinessLogic;
using NexusPoint.Data.Repositories;
using NexusPoint.Utils;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace NexusPoint.Windows
{
    public partial class PrintDocumentsWindow : Window
    {
        private readonly DocumentPrintingService _printingService;
        private readonly ShiftRepository _shiftRepository;

        private CheckDisplayView _selectedCheck = null;
        private CultureInfo _russianCulture = new CultureInfo("ru-RU");

        public PrintDocumentsWindow()
        {
            InitializeComponent();
            var checkRepository = new CheckRepository();
            _shiftRepository = new ShiftRepository();
            var userRepository = new UserRepository();
            var productRepository = new ProductRepository();
            var discountRepository = new DiscountRepository();
            _printingService = new DocumentPrintingService(
                checkRepository, _shiftRepository, userRepository, productRepository, discountRepository
            );
        }

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            try
            {
                var currentShift = _shiftRepository.GetCurrentOpenShift();
                if (currentShift != null) ShiftNumberTextBox.Text = currentShift.ShiftNumber.ToString();
            }
            catch (Exception ex) { ShowError($"Не удалось определить тек. смену: {ex.Message}"); }

            CheckNumberTextBox.Focus();
            UpdateActionButtonsState();
        }

        private async void SearchTextBox_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter) { await FindCheckAsync(); e.Handled = true; }
        }

        private async void FindCheckButton_Click(object sender, RoutedEventArgs e)
        {
            await FindCheckAsync();
        }

        private async Task FindCheckAsync()
        {
            ClearError(); ClearDisplay();
            if (!int.TryParse(CheckNumberTextBox.Text, out int checkNumber) || checkNumber <= 0) { ShowError("Введите корректный номер чека."); return; }
            if (!int.TryParse(ShiftNumberTextBox.Text, out int shiftNumber) || shiftNumber <= 0) { ShowError("Введите корректный номер смены."); return; }

            var foundCheckView = await _printingService.FindCheckAsync(checkNumber, shiftNumber);

            if (foundCheckView == null)
            {
                ShowError($"Чек №{checkNumber} в смене №{shiftNumber} не найден.");
            }
            else
            {
                ChecksListView.ItemsSource = new List<CheckDisplayView> { foundCheckView };
                ChecksListView.SelectedIndex = 0;
                _selectedCheck = foundCheckView;
                UpdateActionButtonsState();
            }
        }

        private async void PrintLastCheckCopyButton_Click(object sender, RoutedEventArgs e)
        {
            ClearError(); ClearDisplay();

            var lastCheckView = await _printingService.GetLastCheckAsync();

            if (lastCheckView == null)
            {
                ShowError("Не найдено ни одного чека.");
                UpdateActionButtonsState();
            }
            else
            {
                ChecksListView.ItemsSource = new List<CheckDisplayView> { lastCheckView };
                ChecksListView.SelectedIndex = 0;
                _selectedCheck = lastCheckView;
                UpdateActionButtonsState();
                await PrintSelectedCheckCopyAsync();
            }
        }
        private void UpdateActionButtonsState()
        {
            bool isCheckSelected = _selectedCheck != null;
            bool isSaleCheckSelected = isCheckSelected && !_selectedCheck.IsReturn;
            PrintCopyButton.IsEnabled = isCheckSelected;
            PrintTovarnyCheckButton.IsEnabled = isSaleCheckSelected;
            PrintDiscountDetailsButton.IsEnabled = isCheckSelected && _selectedCheck.DiscountAmount > 0;
        }
        private void ChecksListView_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            _selectedCheck = ChecksListView.SelectedItem as CheckDisplayView;
            UpdateActionButtonsState();
        }
        private async void PrintCopyButton_Click(object sender, RoutedEventArgs e)
        {
            await PrintSelectedCheckCopyAsync();
        }

        private async void PrintTovarnyCheckButton_Click(object sender, RoutedEventArgs e)
        {
            await PrintSelectedTovarnyCheckAsync();
        }

        private async void PrintDiscountDetailsButton_Click(object sender, RoutedEventArgs e)
        {
            await PrintSelectedDiscountDetailsAsync();
        }
        private async Task PrintSelectedCheckCopyAsync()
        {
            if (_selectedCheck != null)
            {
                try
                {
                    ShowError("Формирование копии чека...", isInfo: true);
                    string content = await _printingService.FormatCheckCopyAsync(_selectedCheck);
                    PrinterService.Print($"Копия чека №{_selectedCheck.CheckNumber}", content);
                    ShowError("Копия чека 'отправлена на печать'.", isInfo: true);
                }
                catch (Exception ex) { ShowError($"Ошибка печати копии: {ex.Message}"); }
                finally { if (StatusText.Text == "Формирование копии чека...") ClearError(); }
            }
            else { ShowError("Сначала найдите или выберите чек."); }
        }

        private async Task PrintSelectedTovarnyCheckAsync()
        {
            if (_selectedCheck != null && !_selectedCheck.IsReturn)
            {
                try
                {
                    ShowError("Формирование товарного чека...", isInfo: true);
                    string content = await _printingService.FormatTovarnyCheckAsync(_selectedCheck);
                    PrinterService.Print($"Товарный чек №{_selectedCheck.CheckNumber}", content);
                    ShowError("Товарный чек 'отправлен на печать'.", isInfo: true);
                }
                catch (Exception ex) { ShowError($"Ошибка печати товарного чека: {ex.Message}"); }
                finally { if (StatusText.Text == "Формирование товарного чека...") ClearError(); }
            }
            else { ShowError("Выберите чек ПРОДАЖИ для печати товарного чека."); }
        }

        private async Task PrintSelectedDiscountDetailsAsync()
        {
            if (_selectedCheck != null)
            {
                try
                {
                    ShowError("Формирование расшифровки скидок...", isInfo: true);
                    string content = await _printingService.FormatDiscountDetailsAsync(_selectedCheck);
                    PrinterService.Print($"Скидки к чеку №{_selectedCheck.CheckNumber}", content);
                    ShowError("Расшифровка скидок 'отправлена на печать'.", isInfo: true);
                }
                catch (Exception ex) { ShowError($"Ошибка печати скидок: {ex.Message}"); }
                finally { if (StatusText.Text == "Формирование расшифровки скидок...") ClearError(); }
            }
            else { ShowError("Сначала найдите или выберите чек."); }
        }
        private void ClearDisplay()
        {
            ChecksListView.ItemsSource = null;
            _selectedCheck = null;
            UpdateActionButtonsState();
        }
        private void ShowError(string message, bool isInfo = false) { StatusText.Text = message; StatusText.Foreground = isInfo ? System.Windows.Media.Brushes.Blue : System.Windows.Media.Brushes.Red; }
        private void ClearError() { StatusText.Text = string.Empty; }
    }
}