using NexusPoint.BusinessLogic;
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
    public partial class AdjustStockWindow : Window
    {
        // Заменяем репозитории на менеджеры
        private readonly ProductManager _productManager;
        private readonly StockManager _stockManager;

        private Product _foundProduct = null;
        private decimal _currentStock = 0m;

        // Конструктор принимает менеджеры
        public AdjustStockWindow(ProductManager productManager, StockManager stockManager)
        {
            InitializeComponent();
            _productManager = productManager ?? throw new ArgumentNullException(nameof(productManager));
            _stockManager = stockManager ?? throw new ArgumentNullException(nameof(stockManager));
        }

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            ItemInputTextBox.Focus();
        }

        // --- Поиск товара (обновлено) ---
        private void ItemInputTextBox_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter && !string.IsNullOrWhiteSpace(ItemInputTextBox.Text))
            {
                FindProduct(); // Вызываем обновленный метод
                e.Handled = true;
            }
        }

        private void FindItemButton_Click(object sender, RoutedEventArgs e)
        {
            FindProduct(); // Вызываем обновленный метод
        }

        private void FindProduct()
        {
            ClearError();
            ClearProductInfo();

            string codeOrBarcode = ItemInputTextBox.Text.Trim();
            if (string.IsNullOrWhiteSpace(codeOrBarcode)) { ShowError("Введите код или штрих-код товара."); return; }

            // Используем ProductManager для поиска
            _foundProduct = _productManager.FindByCodeOrBarcodeInternal(codeOrBarcode);

            if (_foundProduct == null)
            {
                ShowError($"Товар с кодом/ШК '{codeOrBarcode}' не найден.");
                return;
            }

            // Используем StockManager для получения остатка
            _currentStock = _stockManager.GetStockQuantityByProductId(_foundProduct.ProductId);
            // Обработка ошибки получения остатка произойдет внутри менеджера (MessageBox)

            // Отображаем инфо и активируем панель корректировки
            DisplayProductInfo();
            AdjustmentGroup.IsEnabled = true;
            ApplyButton.IsEnabled = true;
            QuantityTextBox.Focus();
        }

        // --- Отображение/Очистка UI (без изменений) ---
        private void DisplayProductInfo()
        {
            if (_foundProduct == null) return;
            ProductNameText.Text = _foundProduct.Name;
            CurrentStockText.Text = _currentStock.ToString("N", CultureInfo.CurrentCulture);
            ProductInfoGroup.Visibility = Visibility.Visible;
        }

        private void ClearProductInfo()
        {
            _foundProduct = null; _currentStock = 0m;
            ProductInfoGroup.Visibility = Visibility.Collapsed; AdjustmentGroup.IsEnabled = false;
            ApplyButton.IsEnabled = false; QuantityTextBox.Clear(); ReasonTextBox.Clear();
            AddRadioButton.IsChecked = true; ProductNameText.Text = ""; CurrentStockText.Text = "";
        }

        // --- Валидация ввода количества (без изменений) ---
        private void QuantityTextBox_PreviewTextInput(object sender, TextCompositionEventArgs e)
        {
            TextBox textBox = sender as TextBox; string currentText = textBox.Text.Insert(textBox.CaretIndex, e.Text);
            string decimalSeparator = CultureInfo.CurrentCulture.NumberFormat.NumberDecimalSeparator;
            bool allowNegative = SubtractRadioButton.IsChecked == true || SetRadioButton.IsChecked == true;
            string pattern = allowNegative ? $@"^-?\d*({Regex.Escape(decimalSeparator)}\d*)?$" : $@"^\d*({Regex.Escape(decimalSeparator)}?\d*)?$";
            Regex regex = new Regex(pattern);
            if (!regex.IsMatch(currentText)) e.Handled = true;
        }


        // --- Применение корректировки (обновлено) ---
        private void ApplyButton_Click(object sender, RoutedEventArgs e)
        {
            ClearError();

            if (_foundProduct == null) { ShowError("Сначала найдите товар."); return; }
            if (!decimal.TryParse(QuantityTextBox.Text, NumberStyles.Any, CultureInfo.CurrentCulture, out decimal quantityValue))
            {
                ShowError("Введите корректное числовое значение количества."); QuantityTextBox.Focus(); QuantityTextBox.SelectAll();
                return;
            }

            bool isAdd = AddRadioButton.IsChecked == true;
            bool isSubtract = SubtractRadioButton.IsChecked == true;
            bool isSet = SetRadioButton.IsChecked == true;

            // Проверка на ПОЛОЖИТЕЛЬНОЕ значение для приемки/списания
            if ((isAdd || isSubtract) && quantityValue <= 0)
            {
                ShowError("Для приемки или списания количество должно быть положительным.");
                QuantityTextBox.Focus(); QuantityTextBox.SelectAll();
                return;
            }
            // Проверка на НЕОТРИЦАТЕЛЬНОЕ значение для установки
            if (isSet && quantityValue < 0)
            {
                ShowError("Устанавливаемый остаток не может быть отрицательным.");
                QuantityTextBox.Focus(); QuantityTextBox.SelectAll();
                return;
            }
            // Проверка на достаточность остатка при списании (дополнительная проверка на уровне UI)
            if (isSubtract && quantityValue > _currentStock)
            {
                ShowError($"Нельзя списать {quantityValue}. Текущий остаток: {_currentStock}.");
                QuantityTextBox.Focus(); QuantityTextBox.SelectAll();
                return;
            }


            // Вызываем соответствующий метод StockManager
            bool success = false;
            string operationDescription = "";

            if (isAdd)
            {
                success = _stockManager.AdjustStockQuantity(_foundProduct.ProductId, quantityValue, true);
                operationDescription = $"Приемка {quantityValue}";
            }
            else if (isSubtract)
            {
                // Дополнительная проверка уже была выше, но менеджер ее повторит
                success = _stockManager.AdjustStockQuantity(_foundProduct.ProductId, quantityValue, false);
                operationDescription = $"Списание {quantityValue}";
            }
            else // isSet
            {
                success = _stockManager.SetStockQuantity(_foundProduct.ProductId, quantityValue);
                operationDescription = $"Установка остатка = {quantityValue}";
            }

            // Обрабатываем результат
            if (success)
            {
                MessageBox.Show($"Остаток для товара '{_foundProduct.Name}' успешно скорректирован.\nОперация: {operationDescription}",
                                "Успех", MessageBoxButton.OK, MessageBoxImage.Information);
                // string reason = ReasonTextBox.Text.Trim(); // Причину пока никуда не пишем
                this.DialogResult = true; // Закрываем окно
            }
            // Сообщение об ошибке покажет менеджер
            // else { // Ошибка уже показана менеджером
            // ShowError("Не удалось обновить остаток."); // Можно добавить общее сообщение, если менеджер не показал
            // }
        }


        // --- Показ/Скрытие ошибки (остаются) ---
        private void ShowError(string message) { ErrorText.Text = message; ErrorText.Visibility = Visibility.Visible; }
        private void ClearError() { ErrorText.Text = string.Empty; ErrorText.Visibility = Visibility.Collapsed; }
    }
}