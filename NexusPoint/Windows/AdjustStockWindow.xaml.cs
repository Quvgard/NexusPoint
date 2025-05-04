using NexusPoint.Data.Repositories;
using NexusPoint.Models;
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
    /// Логика взаимодействия для AdjustStockWindow.xaml
    /// </summary>
    public partial class AdjustStockWindow : Window
    {
        private readonly ProductRepository _productRepository;
        private readonly StockItemRepository _stockItemRepository;
        // CashDrawerOperationRepository может понадобиться, если приемка/списание должны отражаться в кассе
        // private readonly CashDrawerOperationRepository _cashDrawerRepository;

        private Product _foundProduct = null; // Найденный товар
        private decimal _currentStock = 0m;   // Его текущий остаток

        public AdjustStockWindow()
        {
            InitializeComponent();
            _productRepository = new ProductRepository();
            _stockItemRepository = new StockItemRepository();
        }

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            ItemInputTextBox.Focus();
        }

        // --- Поиск товара ---
        private void ItemInputTextBox_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter && !string.IsNullOrWhiteSpace(ItemInputTextBox.Text))
            {
                FindProduct();
                e.Handled = true;
            }
        }

        private void FindItemButton_Click(object sender, RoutedEventArgs e)
        {
            FindProduct();
        }

        private void FindProduct()
        {
            ClearError();
            ClearProductInfo(); // Сбрасываем предыдущий результат

            string codeOrBarcode = ItemInputTextBox.Text.Trim();
            if (string.IsNullOrWhiteSpace(codeOrBarcode))
            {
                ShowError("Введите код или штрих-код товара.");
                return;
            }

            try
            {
                _foundProduct = _productRepository.FindProductByCodeOrBarcode(codeOrBarcode);

                if (_foundProduct == null)
                {
                    ShowError($"Товар с кодом/ШК '{codeOrBarcode}' не найден.");
                    return;
                }

                // Товар найден, получаем остаток
                // Важно: GetStockQuantity вернет 0, если записи StockItem нет.
                // Для корректировки это нормально, если мы делаем приемку (Add) или установку (Set).
                // Но для списания (Subtract) нужно, чтобы запись существовала и остаток был > 0.
                _currentStock = _stockItemRepository.GetStockQuantity(_foundProduct.ProductId);

                // Отображаем инфо и активируем панель корректировки
                DisplayProductInfo();
                AdjustmentGroup.IsEnabled = true;
                ApplyButton.IsEnabled = true; // Кнопку Применить активируем сразу
                QuantityTextBox.Focus(); // Фокус на ввод количества

            }
            catch (Exception ex)
            {
                ShowError($"Ошибка при поиске товара: {ex.Message}");
                System.Diagnostics.Debug.WriteLine($"Find item error: {ex}");
            }
        }

        // Отображение инфо о товаре
        private void DisplayProductInfo()
        {
            if (_foundProduct == null) return;

            ProductNameText.Text = _foundProduct.Name;
            CurrentStockText.Text = _currentStock.ToString("N", CultureInfo.CurrentCulture); // Форматируем остаток
            ProductInfoGroup.Visibility = Visibility.Visible;
        }

        // Очистка инфо о товаре и контролов корректировки
        private void ClearProductInfo()
        {
            _foundProduct = null;
            _currentStock = 0m;
            ProductInfoGroup.Visibility = Visibility.Collapsed;
            AdjustmentGroup.IsEnabled = false;
            ApplyButton.IsEnabled = false;
            QuantityTextBox.Clear();
            ReasonTextBox.Clear();
            AddRadioButton.IsChecked = true; // Сброс на тип по умолчанию
            ProductNameText.Text = "";
            CurrentStockText.Text = "";
        }

        // Валидация ввода количества
        private void QuantityTextBox_PreviewTextInput(object sender, TextCompositionEventArgs e)
        {
            TextBox textBox = sender as TextBox;
            string currentText = textBox.Text.Insert(textBox.CaretIndex, e.Text);
            string decimalSeparator = CultureInfo.CurrentCulture.NumberFormat.NumberDecimalSeparator;
            // Разрешаем опциональный знак минуса ТОЛЬКО для типа "Списание" или в начале строки
            // ИЛИ для установки нового значения (Set)
            bool allowNegative = SubtractRadioButton.IsChecked == true || SetRadioButton.IsChecked == true;
            string negativeSign = allowNegative ? $@"(?!^-)-|(?<!^)-" : "-"; // Запретить минус кроме как в начале
                                                                             // Простой вариант: разрешаем минус всегда, валидируем потом
                                                                             // string pattern = $@"^-?\d*({Regex.Escape(decimalSeparator)}?\d*)?$";

            // Более точный паттерн, чтобы не разрешать "--" или "1-2"
            string pattern = allowNegative ? $@"^-?\d*({Regex.Escape(decimalSeparator)}\d*)?$" : $@"^\d*({Regex.Escape(decimalSeparator)}?\d*)?$";

            Regex regex = new Regex(pattern);

            if (!regex.IsMatch(currentText))
            {
                e.Handled = true;
            }
        }


        // --- Применение корректировки ---
        private void ApplyButton_Click(object sender, RoutedEventArgs e)
        {
            ClearError();

            if (_foundProduct == null)
            {
                ShowError("Сначала найдите товар.");
                return;
            }

            if (!decimal.TryParse(QuantityTextBox.Text, NumberStyles.Any, CultureInfo.CurrentCulture, out decimal quantityValue))
            {
                ShowError("Введите корректное числовое значение количества.");
                QuantityTextBox.Focus();
                QuantityTextBox.SelectAll();
                return;
            }

            // Определяем тип операции
            bool isAdd = AddRadioButton.IsChecked == true;
            bool isSubtract = SubtractRadioButton.IsChecked == true;
            bool isSet = SetRadioButton.IsChecked == true;

            // Дополнительная валидация
            if ((isAdd || isSubtract) && quantityValue <= 0)
            {
                ShowError("Для приемки или списания количество должно быть положительным.");
                QuantityTextBox.Focus();
                QuantityTextBox.SelectAll();
                return;
            }
            if (isSet && quantityValue < 0)
            {
                ShowError("Устанавливаемый остаток не может быть отрицательным.");
                QuantityTextBox.Focus();
                QuantityTextBox.SelectAll();
                return;
            }
            if (isSubtract && quantityValue > _currentStock)
            {
                ShowError($"Нельзя списать {quantityValue}. Текущий остаток: {_currentStock}.");
                QuantityTextBox.Focus();
                QuantityTextBox.SelectAll();
                return;
            }


            try
            {
                bool success = false;
                string operationDescription = "";

                if (isAdd)
                {
                    // Проверяем, существует ли запись StockItem, если нет - создаем
                    _stockItemRepository.EnsureStockItemExists(_foundProduct.ProductId, null, null); // null т.к. нет транзакции тут
                    success = _stockItemRepository.UpdateStockQuantity(_foundProduct.ProductId, quantityValue); // Положительное значение
                    operationDescription = $"Приемка {quantityValue}";
                }
                else if (isSubtract)
                {
                    // Убедимся что запись StockItem существует перед списанием
                    var stockItem = _stockItemRepository.GetStockItem(_foundProduct.ProductId);
                    if (stockItem == null || stockItem.Quantity < quantityValue)
                    {
                        ShowError($"Недостаточно остатка для списания (Текущий: {stockItem?.Quantity ?? 0})");
                        return;
                    }

                    success = _stockItemRepository.UpdateStockQuantity(_foundProduct.ProductId, -quantityValue); // Отрицательное значение
                    operationDescription = $"Списание {-quantityValue}";
                }
                else if (isSet)
                {
                    // Проверяем, существует ли запись StockItem, если нет - создаем
                    _stockItemRepository.EnsureStockItemExists(_foundProduct.ProductId, null, null);
                    success = _stockItemRepository.SetStockQuantity(_foundProduct.ProductId, quantityValue);
                    operationDescription = $"Установка остатка = {quantityValue}";
                }

                if (success)
                {
                    MessageBox.Show($"Остаток для товара '{_foundProduct.Name}' успешно скорректирован.\nОперация: {operationDescription}",
                                    "Успех", MessageBoxButton.OK, MessageBoxImage.Information);

                    // Опционально: можно записать комментарий/причину куда-нибудь (если нужно)
                    // string reason = ReasonTextBox.Text.Trim();

                    this.DialogResult = true; // Закрываем окно после успеха
                }
                else
                {
                    ShowError("Не удалось обновить остаток. Проверьте данные или остаток на складе.");
                }
            }
            catch (Exception ex)
            {
                ShowError($"Ошибка при корректировке остатка: {ex.Message}");
                System.Diagnostics.Debug.WriteLine($"Adjust stock error: {ex}");
            }
        }


        // Показ/Скрытие ошибки
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