using NexusPoint.BusinessLogic;
using NexusPoint.Data.Repositories;
using NexusPoint.Models;
using NexusPoint.Utils;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
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
            UpdateActionButtonsState(); // Кнопки изначально неактивны
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
            ClearError(); ClearDisplay(); // ClearDisplay сбросит _selectedCheck в null
            if (!int.TryParse(CheckNumberTextBox.Text, out int checkNumber) || checkNumber <= 0) { ShowError("Введите корректный номер чека."); return; }
            if (!int.TryParse(ShiftNumberTextBox.Text, out int shiftNumber) || shiftNumber <= 0) { ShowError("Введите корректный номер смены."); return; }

            var foundCheckView = await _printingService.FindCheckAsync(checkNumber, shiftNumber);

            if (foundCheckView == null)
            {
                ShowError($"Чек №{checkNumber} в смене №{shiftNumber} не найден.");
                // _selectedCheck уже null после ClearDisplay()
            }
            else
            {
                ChecksListView.ItemsSource = new List<CheckDisplayView> { foundCheckView };
                // --- ИЗМЕНЕНИЕ: Явно устанавливаем _selectedCheck и обновляем кнопки ---
                ChecksListView.SelectedIndex = 0;
                _selectedCheck = foundCheckView; // Устанавливаем выбранный чек
                UpdateActionButtonsState();      // Обновляем состояние кнопок СРАЗУ
                // --- КОНЕЦ ИЗМЕНЕНИЯ ---
            }
        }

        private async void PrintLastCheckCopyButton_Click(object sender, RoutedEventArgs e)
        {
            ClearError(); ClearDisplay(); // Сбрасываем предыдущий выбор

            var lastCheckView = await _printingService.GetLastCheckAsync();

            if (lastCheckView == null)
            {
                ShowError("Не найдено ни одного чека.");
                // _selectedCheck уже null
                UpdateActionButtonsState(); // Обновляем кнопки (будут неактивны)
            }
            else
            {
                ChecksListView.ItemsSource = new List<CheckDisplayView> { lastCheckView };
                // --- ИЗМЕНЕНИЕ: Устанавливаем _selectedCheck и обновляем кнопки ДО печати ---
                ChecksListView.SelectedIndex = 0;
                _selectedCheck = lastCheckView; // Устанавливаем выбранный чек
                UpdateActionButtonsState();     // Обновляем состояние кнопок

                // Теперь можно безопасно вызвать печать
                await PrintSelectedCheckCopyAsync();
                // --- КОНЕЦ ИЗМЕНЕНИЯ ---
            }
        }

        // Обновление состояния кнопок (без изменений в логике, но теперь вызывается корректно)
        private void UpdateActionButtonsState()
        {
            bool isCheckSelected = _selectedCheck != null;
            bool isSaleCheckSelected = isCheckSelected && !_selectedCheck.IsReturn;
            PrintCopyButton.IsEnabled = isCheckSelected;
            PrintTovarnyCheckButton.IsEnabled = isSaleCheckSelected;
            PrintDiscountDetailsButton.IsEnabled = isCheckSelected && _selectedCheck.DiscountAmount > 0;
        }

        // Выбор чека в списке (без изменений)
        private void ChecksListView_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            _selectedCheck = ChecksListView.SelectedItem as CheckDisplayView;
            UpdateActionButtonsState(); // Обновляем кнопки при ручном выборе
        }


        // --- Кнопки действий ---
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


        // --- Асинхронные методы печати (проверка _selectedCheck остается) ---
        private async Task PrintSelectedCheckCopyAsync()
        {
            // Проверка _selectedCheck здесь все еще нужна на случай прямого вызова
            if (_selectedCheck != null)
            {
                try
                {
                    ShowError("Формирование копии чека...", isInfo: true); // Инфо сообщение
                    string content = await _printingService.FormatCheckCopyAsync(_selectedCheck);
                    PrinterService.Print($"Копия чека №{_selectedCheck.CheckNumber}", content);
                    ShowError("Копия чека 'отправлена на печать'.", isInfo: true);
                }
                catch (Exception ex) { ShowError($"Ошибка печати копии: {ex.Message}"); }
                finally { if (StatusText.Text == "Формирование копии чека...") ClearError(); } // Очищаем, если не было ошибки
            }
            else { ShowError("Сначала найдите или выберите чек."); } // Это сообщение теперь не должно появляться для "Копии последнего"
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


        // --- Очистка и сообщения ---
        private void ClearDisplay()
        {
            ChecksListView.ItemsSource = null;
            _selectedCheck = null;
            UpdateActionButtonsState(); // Обновляем кнопки при очистке
        }
        private void ShowError(string message, bool isInfo = false) { StatusText.Text = message; StatusText.Foreground = isInfo ? System.Windows.Media.Brushes.Blue : System.Windows.Media.Brushes.Red; }
        private void ClearError() { StatusText.Text = string.Empty; }
    }
}