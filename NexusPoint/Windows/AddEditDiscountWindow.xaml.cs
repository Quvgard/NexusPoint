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
    public partial class AddEditDiscountWindow : Window
    {
        // --- Поля класса ---
        // Заменяем репозитории на менеджеры
        private readonly DiscountManager _discountManager;
        private readonly ProductManager _productManager; // Нужен для поиска товаров

        private readonly Discount _originalDiscount;
        private bool IsEditMode => _originalDiscount != null;
        private Product _requiredProduct = null;
        private Product _giftProduct = null;

        // --- Конструкторы ---
        // Принимаем менеджеры через конструктор
        public AddEditDiscountWindow(DiscountManager discountManager, ProductManager productManager)
        {
            InitializeComponent();
            _discountManager = discountManager ?? throw new ArgumentNullException(nameof(discountManager));
            _productManager = productManager ?? throw new ArgumentNullException(nameof(productManager));
            _originalDiscount = null;
            this.Title = "Добавление акции";
        }

        public AddEditDiscountWindow(DiscountManager discountManager, ProductManager productManager, Discount discountToEdit)
            : this(discountManager, productManager) // Вызов основного конструктора
        {
            _originalDiscount = discountToEdit ?? throw new ArgumentNullException(nameof(discountToEdit));
            this.Title = "Редактирование акции";
            // LoadDiscountData() будет вызван в Window_Loaded
        }

        // --- Загрузка окна ---
        private async void Window_Loaded(object sender, RoutedEventArgs e)
        {
            if (IsEditMode)
            {
                await LoadDiscountDataAsync(); // Загружаем асинхронно
            }
            else
            {
                TypeComboBox.SelectedIndex = 0; // Percentage по умолчанию
                IsActiveCheckBox.IsChecked = true;
                UpdateUIVisibility(); // Обновляем видимость сразу
            }
            NameTextBox.Focus();
        }

        // --- Асинхронная загрузка данных для редактирования ---
        private async Task LoadDiscountDataAsync()
        {
            if (_originalDiscount == null) return;

            // Заполнение общих полей
            NameTextBox.Text = _originalDiscount.Name;
            DescriptionTextBox.Text = _originalDiscount.Description; // Добавлено поле Описание
            StartDatePicker.SelectedDate = _originalDiscount.StartDate;
            EndDatePicker.SelectedDate = _originalDiscount.EndDate;
            IsActiveCheckBox.IsChecked = _originalDiscount.IsActive;

            // Выбор типа
            foreach (ComboBoxItem item in TypeComboBox.Items) { if (item.Content.ToString() == _originalDiscount.Type) { TypeComboBox.SelectedItem = item; break; } }
            if (TypeComboBox.SelectedItem == null) TypeComboBox.SelectedIndex = 0;

            // Загрузка ID/Значений в поля
            ValueTextBox.Text = _originalDiscount.Value?.ToString(CultureInfo.CurrentCulture);
            RequiredQuantityNTextBox.Text = _originalDiscount.RequiredQuantityN?.ToString();
            GiftQuantityMTextBox.Text = _originalDiscount.GiftQuantityM?.ToString();
            NthItemNumberTextBox.Text = _originalDiscount.NthItemNumber?.ToString();
            CheckAmountThresholdTextBox.Text = _originalDiscount.CheckAmountThreshold?.ToString(CultureInfo.CurrentCulture);

            // Установка RadioButton и значений для N-ного и Чека
            if (_originalDiscount.Type == "Скидка на N-ный")
            {
                if (_originalDiscount.IsNthDiscountPercentage) NthDiscountPercentageRadio.IsChecked = true; else NthDiscountAmountRadio.IsChecked = true;
                NthValueTextBox.Text = _originalDiscount.Value?.ToString(CultureInfo.CurrentCulture);
            }
            else if (_originalDiscount.Type == "Скидка на сумму чека")
            {
                if (_originalDiscount.IsCheckDiscountPercentage) CheckDiscountPercentageRadio.IsChecked = true; else CheckDiscountAmountRadio.IsChecked = true;
                CheckValueTextBox.Text = _originalDiscount.Value?.ToString(CultureInfo.CurrentCulture);
            }

            // Асинхронная загрузка связанных товаров с помощью ProductManager
            var requiredTask = LoadProductInfoAsync(_originalDiscount.RequiredProductId, RequiredProductTextBox, RequiredProductNameText, true);
            var giftTask = LoadProductInfoAsync(_originalDiscount.GiftProductId, GiftProductTextBox, GiftProductNameText, false);
            await Task.WhenAll(requiredTask, giftTask);

            UpdateUIVisibility();
        }

        // Вспомогательный метод для асинхронной загрузки инфо о товаре (использует _productManager)
        private async Task LoadProductInfoAsync(int? productId, TextBox codeTextBox, TextBlock nameTextBlock, bool isRequiredField)
        {
            if (!productId.HasValue)
            {
                // Очищаем поля, если ID нет
                codeTextBox.Text = string.Empty;
                nameTextBlock.Text = string.Empty;
                if (isRequiredField) _requiredProduct = null; else _giftProduct = null;
                return;
            }

            Product product = null;
            // Используем ProductManager для поиска по ID
            // Обернем в Task.Run, т.к. GetProductsByIdsAsync ожидает список
            var products = await _productManager.GetProductsByIdsAsync(new List<int> { productId.Value });
            product = products.FirstOrDefault();

            if (product != null)
            {
                codeTextBox.Text = product.ProductCode ?? productId.Value.ToString();
                nameTextBlock.Text = product.Name;
                if (isRequiredField) _requiredProduct = product; else _giftProduct = product;
            }
            else
            {
                codeTextBox.Text = productId.Value.ToString(); // Показываем ID
                nameTextBlock.Text = "<Товар удален/не найден>";
                if (isRequiredField) _requiredProduct = null; else _giftProduct = null;
            }
        }


        // --- UI Logic (видимость, валидация) - остаются без существенных изменений ---
        private void TypeComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e) => UpdateUIVisibility();

        private void UpdateUIVisibility()
        {
            if (!IsLoaded) return;
            string selectedType = (TypeComboBox.SelectedItem as ComboBoxItem)?.Content.ToString();
            string currencySymbol = CultureInfo.CurrentCulture.NumberFormat.CurrencySymbol;

            ValuePanel.Visibility = Visibility.Collapsed; RequiredProductPanel.Visibility = Visibility.Collapsed;
            GiftPanel.Visibility = Visibility.Collapsed; NxMPanel.Visibility = Visibility.Collapsed;
            NthPanel.Visibility = Visibility.Collapsed; CheckAmountPanel.Visibility = Visibility.Collapsed;

            switch (selectedType)
            {
                case "Процент": ValuePanel.Visibility = Visibility.Visible; RequiredProductPanel.Visibility = Visibility.Visible; ValueSuffixText.Text = "%"; break;
                case "Сумма":
                case "Фикс. цена": ValuePanel.Visibility = Visibility.Visible; RequiredProductPanel.Visibility = Visibility.Visible; ValueSuffixText.Text = currencySymbol; break;
                case "Подарок": RequiredProductPanel.Visibility = Visibility.Visible; GiftPanel.Visibility = Visibility.Visible; break;
                case "N+M Подарок": RequiredProductPanel.Visibility = Visibility.Visible; GiftPanel.Visibility = Visibility.Visible; NxMPanel.Visibility = Visibility.Visible; break;
                case "Скидка на N-ный": RequiredProductPanel.Visibility = Visibility.Visible; NthPanel.Visibility = Visibility.Visible; UpdateNthSuffix(); break;
                case "Скидка на сумму чека": CheckAmountPanel.Visibility = Visibility.Visible; UpdateCheckSuffix(); break;
            }
            SetupRadioButtonSuffixHandlers();
        }

        private void UpdateNthSuffix() { if (NthValueSuffixText != null) NthValueSuffixText.Text = NthDiscountPercentageRadio.IsChecked == true ? "%" : CultureInfo.CurrentCulture.NumberFormat.CurrencySymbol; }
        private void UpdateCheckSuffix() { if (CheckValueSuffixText != null) CheckValueSuffixText.Text = CheckDiscountPercentageRadio.IsChecked == true ? "%" : CultureInfo.CurrentCulture.NumberFormat.CurrencySymbol; }

        private bool _radioHandlersSetup = false;
        private void SetupRadioButtonSuffixHandlers()
        {
            if (_radioHandlersSetup) return;
            RoutedEventHandler nthHandler = (s, e) => UpdateNthSuffix();
            NthDiscountPercentageRadio.Checked += nthHandler; NthDiscountAmountRadio.Checked += nthHandler;
            RoutedEventHandler checkHandler = (s, e) => UpdateCheckSuffix();
            CheckDiscountPercentageRadio.Checked += checkHandler; CheckDiscountAmountRadio.Checked += checkHandler;
            _radioHandlersSetup = true;
        }

        private void NumericTextBox_PreviewTextInput(object sender, TextCompositionEventArgs e)
        {
            TextBox textBox = sender as TextBox; string currentText = textBox.Text.Insert(textBox.CaretIndex, e.Text);
            string decimalSeparator = CultureInfo.CurrentCulture.NumberFormat.NumberDecimalSeparator;
            string pattern = $@"^\d*({Regex.Escape(decimalSeparator)}?\d*)?$"; Regex regex = new Regex(pattern);
            if (!regex.IsMatch(currentText)) e.Handled = true;
        }
        private void IntegerTextBox_PreviewTextInput(object sender, TextCompositionEventArgs e)
        {
            TextBox textBox = sender as TextBox; string newText = textBox.Text.Insert(textBox.CaretIndex, e.Text);
            Regex regex = new Regex("^[0-9]+$"); if (!regex.IsMatch(newText) && newText != "") e.Handled = true;
        }


        // --- Поиск товаров (обновлено) ---
        private async void FindProductButton_Click(object sender, RoutedEventArgs e)
        {
            Button btn = sender as Button;
            if (btn == null) return;
            bool isRequiredSearch = btn.CommandParameter?.ToString() == "Required";
            TextBox targetTextBox = isRequiredSearch ? RequiredProductTextBox : GiftProductTextBox;
            TextBlock targetNameBlock = isRequiredSearch ? RequiredProductNameText : GiftProductNameText;

            string codeOrBarcode = targetTextBox.Text.Trim();
            if (string.IsNullOrEmpty(codeOrBarcode)) return;

            ClearError();
            targetNameBlock.Text = "Поиск...";
            Product foundProduct = null;

            try
            {
                // Используем ProductManager для поиска
                // FindByCodeOrBarcodeInternal не асинхронный, но обернем для консистентности
                foundProduct = await Task.Run(() => _productManager.FindByCodeOrBarcodeInternal(codeOrBarcode)); // Используем новый внутренний метод

                if (foundProduct != null)
                {
                    targetNameBlock.Text = foundProduct.Name;
                    targetTextBox.Text = foundProduct.ProductCode; // Обновляем на код
                    if (isRequiredSearch) _requiredProduct = foundProduct; else _giftProduct = foundProduct;
                }
                else
                {
                    targetNameBlock.Text = "<не найден>";
                    ShowError($"Товар с кодом/ШК '{codeOrBarcode}' не найден.");
                    if (isRequiredSearch) _requiredProduct = null; else _giftProduct = null;
                }
            }
            catch (Exception ex)
            {
                targetNameBlock.Text = "<ошибка>";
                ShowError($"Ошибка поиска: {ex.Message}");
                if (isRequiredSearch) _requiredProduct = null; else _giftProduct = null;
            }
        }


        // --- Сохранение (обновлено) ---
        private void SaveButton_Click(object sender, RoutedEventArgs e)
        {
            ClearError();
            if (!ValidateInput()) return; // Валидация остается здесь

            // Создаем или обновляем объект
            Discount discountToSave = IsEditMode ? _originalDiscount : new Discount();

            discountToSave.Name = NameTextBox.Text.Trim();
            discountToSave.Description = DescriptionTextBox.Text.Trim(); // Сохраняем описание
            discountToSave.Type = (TypeComboBox.SelectedItem as ComboBoxItem).Content.ToString();
            discountToSave.StartDate = StartDatePicker.SelectedDate;
            discountToSave.EndDate = EndDatePicker.SelectedDate;
            discountToSave.IsActive = IsActiveCheckBox.IsChecked ?? false;

            // Обнуляем специфичные поля перед заполнением
            discountToSave.Value = null; discountToSave.RequiredProductId = null; discountToSave.GiftProductId = null;
            discountToSave.RequiredQuantityN = null; discountToSave.GiftQuantityM = null; discountToSave.NthItemNumber = null;
            discountToSave.IsNthDiscountPercentage = false; discountToSave.CheckAmountThreshold = null; discountToSave.IsCheckDiscountPercentage = false;

            decimal parsedValue; int parsedInt;

            // Заполняем поля в зависимости от типа
            switch (discountToSave.Type)
            {
                case "Процент":
                case "Сумма":
                case "Фикс. цена":
                    if (decimal.TryParse(ValueTextBox.Text, NumberStyles.Any, CultureInfo.CurrentCulture, out parsedValue)) discountToSave.Value = Math.Round(parsedValue, 2);
                    discountToSave.RequiredProductId = _requiredProduct?.ProductId; break;
                case "Подарок":
                    discountToSave.RequiredProductId = _requiredProduct?.ProductId;
                    discountToSave.GiftProductId = _giftProduct?.ProductId; break; // Должен быть найден по валидации
                case "N+M Подарок":
                    discountToSave.RequiredProductId = _requiredProduct?.ProductId; // Обязателен по валидации
                    discountToSave.GiftProductId = _giftProduct?.ProductId; // Обязателен по валидации
                    if (int.TryParse(RequiredQuantityNTextBox.Text, out int n)) discountToSave.RequiredQuantityN = n;
                    if (int.TryParse(GiftQuantityMTextBox.Text, out int m)) discountToSave.GiftQuantityM = m; break;
                case "Скидка на N-ный":
                    discountToSave.RequiredProductId = _requiredProduct?.ProductId; // Обязателен по валидации
                    if (int.TryParse(NthItemNumberTextBox.Text, out int nth)) discountToSave.NthItemNumber = nth;
                    discountToSave.IsNthDiscountPercentage = NthDiscountPercentageRadio.IsChecked == true;
                    if (decimal.TryParse(NthValueTextBox.Text, NumberStyles.Any, CultureInfo.CurrentCulture, out parsedValue)) discountToSave.Value = Math.Round(parsedValue, 2); break;
                case "Скидка на сумму чека":
                    if (decimal.TryParse(CheckAmountThresholdTextBox.Text, NumberStyles.Any, CultureInfo.CurrentCulture, out parsedValue)) discountToSave.CheckAmountThreshold = Math.Round(parsedValue, 2);
                    discountToSave.IsCheckDiscountPercentage = CheckDiscountPercentageRadio.IsChecked == true;
                    if (decimal.TryParse(CheckValueTextBox.Text, NumberStyles.Any, CultureInfo.CurrentCulture, out parsedValue)) discountToSave.Value = Math.Round(parsedValue, 2); break;
            }

            // Сохранение через DiscountManager
            bool success;
            if (IsEditMode) { success = _discountManager.UpdateDiscount(discountToSave); }
            else { success = _discountManager.AddDiscount(discountToSave); }

            if (success) { this.DialogResult = true; }
            // Сообщение об ошибке покажется из менеджера
        }

        // --- Метод валидации (остается здесь) ---
        private bool ValidateInput()
        {
            string name = NameTextBox.Text.Trim(); string type = (TypeComboBox.SelectedItem as ComboBoxItem)?.Content.ToString();
            DateTime? startDate = StartDatePicker.SelectedDate; DateTime? endDate = EndDatePicker.SelectedDate;

            if (string.IsNullOrWhiteSpace(name)) { ShowError("Введите название акции."); NameTextBox.Focus(); return false; }
            if (string.IsNullOrEmpty(type)) { ShowError("Выберите тип акции."); TypeComboBox.Focus(); return false; }
            if (startDate.HasValue && endDate.HasValue && endDate.Value < startDate.Value) { ShowError("Дата окончания не может быть раньше даты начала."); EndDatePicker.Focus(); return false; }

            decimal parsedValue; int parsedInt;

            switch (type)
            {
                case "Процент":
                case "Сумма":
                case "Фикс. цена":
                    if (!decimal.TryParse(ValueTextBox.Text, NumberStyles.Any, CultureInfo.CurrentCulture, out parsedValue) || parsedValue <= 0) { ShowError($"Введите корректное положительное значение для типа '{type}'."); ValueTextBox.Focus(); return false; }
                    if (type == "Процент" && parsedValue > 100) { ShowError("Процент скидки не может быть больше 100."); ValueTextBox.Focus(); return false; }
                    if (!string.IsNullOrWhiteSpace(RequiredProductTextBox.Text) && _requiredProduct == null) { ShowError($"Товар-условие '{RequiredProductTextBox.Text}' не найден. Нажмите 'Найти'."); RequiredProductTextBox.Focus(); return false; }
                    break;
                case "Подарок":
                    if (!string.IsNullOrWhiteSpace(RequiredProductTextBox.Text) && _requiredProduct == null) { ShowError($"Товар-условие '{RequiredProductTextBox.Text}' не найден. Нажмите 'Найти'."); RequiredProductTextBox.Focus(); return false; }
                    if (_giftProduct == null) { ShowError("Необходимо указать и найти товар-подарок."); GiftProductTextBox.Focus(); return false; }
                    break;
                case "N+M Подарок":
                    if (_requiredProduct == null) { ShowError("Необходимо указать и найти товар-условие (N)."); RequiredProductTextBox.Focus(); return false; }
                    if (_giftProduct == null) { ShowError("Необходимо указать и найти товар-подарок (M)."); GiftProductTextBox.Focus(); return false; }
                    if (!int.TryParse(RequiredQuantityNTextBox.Text, out parsedInt) || parsedInt <= 0) { ShowError("Введите корректное целое положительное количество N (купить)."); RequiredQuantityNTextBox.Focus(); return false; }
                    if (!int.TryParse(GiftQuantityMTextBox.Text, out parsedInt) || parsedInt <= 0) { ShowError("Введите корректное целое положительное количество M (подарок)."); GiftQuantityMTextBox.Focus(); return false; }
                    break;
                case "Скидка на N-ный":
                    if (_requiredProduct == null) { ShowError("Необходимо указать и найти товар-условие."); RequiredProductTextBox.Focus(); return false; }
                    if (!int.TryParse(NthItemNumberTextBox.Text, out parsedInt) || parsedInt <= 0) { ShowError("Введите корректный номер N-ного товара (N > 0)."); NthItemNumberTextBox.Focus(); return false; }
                    if (!decimal.TryParse(NthValueTextBox.Text, NumberStyles.Any, CultureInfo.CurrentCulture, out parsedValue) || parsedValue <= 0) { ShowError("Введите корректное положительное значение скидки (процент или сумма)."); NthValueTextBox.Focus(); return false; }
                    if (NthDiscountPercentageRadio.IsChecked == true && parsedValue > 100) { ShowError("Процент скидки не может быть больше 100."); NthValueTextBox.Focus(); return false; }
                    break;
                case "Скидка на сумму чека":
                    if (!decimal.TryParse(CheckAmountThresholdTextBox.Text, NumberStyles.Any, CultureInfo.CurrentCulture, out parsedValue) || parsedValue <= 0) { ShowError("Введите корректный положительный порог суммы чека."); CheckAmountThresholdTextBox.Focus(); return false; }
                    if (!decimal.TryParse(CheckValueTextBox.Text, NumberStyles.Any, CultureInfo.CurrentCulture, out parsedValue) || parsedValue <= 0) { ShowError("Введите корректное положительное значение скидки (процент или сумма)."); CheckValueTextBox.Focus(); return false; }
                    if (CheckDiscountPercentageRadio.IsChecked == true && parsedValue > 100) { ShowError("Процент скидки не может быть больше 100."); CheckValueTextBox.Focus(); return false; }
                    break;
            }
            return true; // Валидация пройдена
        }

        // --- Вспомогательные ---
        private void ShowError(string message) { ErrorText.Text = message; ErrorText.Visibility = Visibility.Visible; }
        private void ClearError() { ErrorText.Text = string.Empty; ErrorText.Visibility = Visibility.Collapsed; }
    }
}