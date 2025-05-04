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
    /// Логика взаимодействия для AddEditDiscountWindow.xaml
    /// </summary>
    public partial class AddEditDiscountWindow : Window
    {
        // --- Поля класса ---
        private readonly DiscountRepository _discountRepository;
        private readonly ProductRepository _productRepository;
        private readonly Discount _originalDiscount;
        private bool IsEditMode => _originalDiscount != null;
        private Product _requiredProduct = null;
        private Product _giftProduct = null;

        // --- Конструкторы ---
        public AddEditDiscountWindow()
        {
            InitializeComponent();
            _discountRepository = new DiscountRepository();
            _productRepository = new ProductRepository();
            this.Title = "Добавление акции";
        }
        public AddEditDiscountWindow(Discount discountToEdit) : this()
        {
            _originalDiscount = discountToEdit;
            this.Title = "Редактирование акции";
            // LoadDiscountData() будет вызван в Window_Loaded
        }

        // --- Загрузка окна ---
        private async void Window_Loaded(object sender, RoutedEventArgs e)
        {
            if (IsEditMode)
            {
                await LoadDiscountData(); // Загружаем асинхронно
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
        private async System.Threading.Tasks.Task LoadDiscountData()
        {
            if (_originalDiscount == null) return;

            NameTextBox.Text = _originalDiscount.Name;
            StartDatePicker.SelectedDate = _originalDiscount.StartDate;
            EndDatePicker.SelectedDate = _originalDiscount.EndDate;
            IsActiveCheckBox.IsChecked = _originalDiscount.IsActive;

            // Выбор типа
            foreach (ComboBoxItem item in TypeComboBox.Items)
            {
                if (item.Content.ToString() == _originalDiscount.Type)
                {
                    TypeComboBox.SelectedItem = item;
                    break;
                }
            }
            if (TypeComboBox.SelectedItem == null) TypeComboBox.SelectedIndex = 0; // Fallback

            // Загрузка ID/Значений в поля
            ValueTextBox.Text = _originalDiscount.Value?.ToString(CultureInfo.CurrentCulture);
            RequiredQuantityNTextBox.Text = _originalDiscount.RequiredQuantityN?.ToString();
            GiftQuantityMTextBox.Text = _originalDiscount.GiftQuantityM?.ToString();
            NthItemNumberTextBox.Text = _originalDiscount.NthItemNumber?.ToString();
            CheckAmountThresholdTextBox.Text = _originalDiscount.CheckAmountThreshold?.ToString(CultureInfo.CurrentCulture);

            // Установка RadioButton для N-ного
            if (_originalDiscount.IsNthDiscountPercentage) NthDiscountPercentageRadio.IsChecked = true;
            else NthDiscountAmountRadio.IsChecked = true;
            NthValueTextBox.Text = _originalDiscount.Value?.ToString(CultureInfo.CurrentCulture); // Value используется и здесь

            // Установка RadioButton для скидки на чек
            if (_originalDiscount.IsCheckDiscountPercentage) CheckDiscountPercentageRadio.IsChecked = true;
            else CheckDiscountAmountRadio.IsChecked = true;
            CheckValueTextBox.Text = _originalDiscount.Value?.ToString(CultureInfo.CurrentCulture); // Value используется и здесь

            // Асинхронная загрузка связанных товаров
            var requiredTask = LoadProductInfoAsync(_originalDiscount.RequiredProductId, RequiredProductTextBox, RequiredProductNameText, true);
            var giftTask = LoadProductInfoAsync(_originalDiscount.GiftProductId, GiftProductTextBox, GiftProductNameText, false);
            await System.Threading.Tasks.Task.WhenAll(requiredTask, giftTask);

            UpdateUIVisibility(); // Обновляем видимость после загрузки
        }

        // Вспомогательный метод для асинхронной загрузки инфо о товаре
        private async System.Threading.Tasks.Task LoadProductInfoAsync(int? productId, TextBox codeTextBox, TextBlock nameTextBlock, bool isRequired)
        {
            if (!productId.HasValue) return;
            Product product = null;
            try
            {
                product = await System.Threading.Tasks.Task.Run(() => _productRepository.FindProductById(productId.Value));
            }
            catch { } // Игнорируем ошибки поиска при загрузке

            if (product != null)
            {
                codeTextBox.Text = product.ProductCode ?? productId.Value.ToString(); // Показываем код, если есть
                nameTextBlock.Text = product.Name;
                if (isRequired) _requiredProduct = product;
                else _giftProduct = product;
            }
            else
            {
                codeTextBox.Text = productId.Value.ToString(); // Показываем ID, если товар не найден
                nameTextBlock.Text = "<Товар удален или не найден>";
            }
        }


        // --- Управление видимостью панелей ---
        private void TypeComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            UpdateUIVisibility();
        }

        private void UpdateUIVisibility()
        {
            if (!IsLoaded) return; // Не выполнять до полной загрузки окна

            string selectedType = (TypeComboBox.SelectedItem as ComboBoxItem)?.Content.ToString();
            string currencySymbol = CultureInfo.CurrentCulture.NumberFormat.CurrencySymbol; // Получаем символ валюты

            // Сначала все скрываем
            ValuePanel.Visibility = Visibility.Collapsed;
            RequiredProductPanel.Visibility = Visibility.Collapsed;
            GiftPanel.Visibility = Visibility.Collapsed;
            NxMPanel.Visibility = Visibility.Collapsed;
            NthPanel.Visibility = Visibility.Collapsed;
            CheckAmountPanel.Visibility = Visibility.Collapsed;

            // Показываем нужные панели и устанавливаем суффиксы
            switch (selectedType)
            {
                case "Процент":
                    ValuePanel.Visibility = Visibility.Visible;
                    RequiredProductPanel.Visibility = Visibility.Visible;
                    ValueSuffixText.Text = "%"; // Устанавливаем суффикс
                    break;
                case "Сумма":
                case "Фикс. цена":
                    ValuePanel.Visibility = Visibility.Visible;
                    RequiredProductPanel.Visibility = Visibility.Visible;
                    ValueSuffixText.Text = currencySymbol; // Устанавливаем суффикс
                    break;
                case "Подарок":
                    RequiredProductPanel.Visibility = Visibility.Visible; // Опционально
                    GiftPanel.Visibility = Visibility.Visible;
                    break;
                case "N+M Подарок":
                    RequiredProductPanel.Visibility = Visibility.Visible;
                    GiftPanel.Visibility = Visibility.Visible;
                    NxMPanel.Visibility = Visibility.Visible;
                    break;
                case "Скидка на N-ный":
                    RequiredProductPanel.Visibility = Visibility.Visible;
                    NthPanel.Visibility = Visibility.Visible;
                    // Устанавливаем суффикс для N-ного в зависимости от RadioButton
                    NthValueSuffixText.Text = NthDiscountPercentageRadio.IsChecked == true ? "%" : currencySymbol;
                    break;
                case "Скидка на сумму чека":
                    CheckAmountPanel.Visibility = Visibility.Visible;
                    // Устанавливаем суффикс для скидки на чек в зависимости от RadioButton
                    CheckValueSuffixText.Text = CheckDiscountPercentageRadio.IsChecked == true ? "%" : currencySymbol;
                    break;
            }

            // Добавим обработчики для RadioButton, чтобы менять суффиксы динамически
            SetupRadioButtonSuffixHandlers();
        }

        private bool _radioHandlersSetup = false;
        private void SetupRadioButtonSuffixHandlers()
        {
            if (_radioHandlersSetup) return; // Выполняем только один раз

            string currencySymbol = CultureInfo.CurrentCulture.NumberFormat.CurrencySymbol;

            // --- Обработчик для типа скидки на N-ный товар ---
            RoutedEventHandler nthHandler = (s, e) => {
                if (NthValueSuffixText != null) // Проверка на null на всякий случай
                {
                    NthValueSuffixText.Text = NthDiscountPercentageRadio.IsChecked == true ? "%" : currencySymbol;
                }
            };
            // Привязываем обработчик к обоим RadioButton в группе NthType
            NthDiscountPercentageRadio.Checked += nthHandler;
            NthDiscountAmountRadio.Checked += nthHandler;

            // --- Обработчик для типа скидки на сумму чека ---
            RoutedEventHandler checkHandler = (s, e) => {
                if (CheckValueSuffixText != null)
                {
                    CheckValueSuffixText.Text = CheckDiscountPercentageRadio.IsChecked == true ? "%" : currencySymbol;
                }
            };
            // Привязываем обработчик к обоим RadioButton в группе CheckType
            CheckDiscountPercentageRadio.Checked += checkHandler;
            CheckDiscountAmountRadio.Checked += checkHandler;

            _radioHandlersSetup = true; // Помечаем, что настройка выполнена
        }


        // --- Валидация ввода ---
        private void NumericTextBox_PreviewTextInput(object sender, TextCompositionEventArgs e)
        {
            TextBox textBox = sender as TextBox;
            string currentText = textBox.Text.Insert(textBox.CaretIndex, e.Text);
            string decimalSeparator = CultureInfo.CurrentCulture.NumberFormat.NumberDecimalSeparator;
            // Разрешаем цифры и один разделитель
            string pattern = $@"^\d*({Regex.Escape(decimalSeparator)}?\d*)?$";
            Regex regex = new Regex(pattern);
            if (!regex.IsMatch(currentText)) e.Handled = true;
        }

        private void IntegerTextBox_PreviewTextInput(object sender, TextCompositionEventArgs e)
        {
            // Разрешаем только цифры
            Regex regex = new Regex("^[0-9]+$"); // Только положительные целые
                                                 // Чтобы разрешить ввод в пустой текстбокс, проверяем весь предполагаемый текст
            TextBox textBox = sender as TextBox;
            string newText = textBox.Text.Insert(textBox.CaretIndex, e.Text);
            if (!regex.IsMatch(newText) && newText != "") // Разрешаем пустую строку
            {
                e.Handled = true;
            }
        }


        // --- Поиск товаров ---
        private async void FindProductButton_Click(object sender, RoutedEventArgs e)
        {
            Button btn = sender as Button;
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
                foundProduct = await System.Threading.Tasks.Task.Run(() =>
                    _productRepository.FindProductByCodeOrBarcode(codeOrBarcode));

                if (foundProduct != null)
                {
                    targetNameBlock.Text = foundProduct.Name;
                    targetTextBox.Text = foundProduct.ProductCode; // Обновляем на код
                    if (isRequiredSearch) _requiredProduct = foundProduct;
                    else _giftProduct = foundProduct;
                }
                else
                {
                    targetNameBlock.Text = "<не найден>";
                    ShowError($"Товар с кодом/ШК '{codeOrBarcode}' не найден.");
                    if (isRequiredSearch) _requiredProduct = null;
                    else _giftProduct = null;
                }
            }
            catch (Exception ex)
            {
                targetNameBlock.Text = "<ошибка>";
                ShowError($"Ошибка поиска: {ex.Message}");
                if (isRequiredSearch) _requiredProduct = null;
                else _giftProduct = null;
            }
        }


        // --- Сохранение ---
        private void SaveButton_Click(object sender, RoutedEventArgs e)
        {
            ClearError();
            if (!ValidateInput()) return; // Выносим валидацию в отдельный метод

            // Создаем или обновляем объект
            Discount discountToSave = IsEditMode ? _originalDiscount : new Discount();

            discountToSave.Name = NameTextBox.Text.Trim();
            discountToSave.Type = (TypeComboBox.SelectedItem as ComboBoxItem).Content.ToString();
            discountToSave.StartDate = StartDatePicker.SelectedDate;
            discountToSave.EndDate = EndDatePicker.SelectedDate;
            discountToSave.IsActive = IsActiveCheckBox.IsChecked ?? false;

            // Заполняем специфичные для типа поля
            discountToSave.Value = null;
            discountToSave.RequiredProductId = null;
            discountToSave.GiftProductId = null;
            discountToSave.RequiredQuantityN = null;
            discountToSave.GiftQuantityM = null;
            discountToSave.NthItemNumber = null;
            discountToSave.IsNthDiscountPercentage = false;
            discountToSave.CheckAmountThreshold = null;
            discountToSave.IsCheckDiscountPercentage = false;

            decimal parsedValue; // Для TryParse

            switch (discountToSave.Type)
            {
                case "Процент":
                case "Сумма":
                case "Фикс. цена":
                    if (decimal.TryParse(ValueTextBox.Text, NumberStyles.Any, CultureInfo.CurrentCulture, out parsedValue))
                        discountToSave.Value = Math.Round(parsedValue, 2);
                    discountToSave.RequiredProductId = _requiredProduct?.ProductId; // Может быть null
                    break;
                case "Подарок":
                    discountToSave.RequiredProductId = _requiredProduct?.ProductId; // Может быть null
                    discountToSave.GiftProductId = _giftProduct?.ProductId; // Должен быть найден
                    break;
                case "N+M Подарок":
                    discountToSave.RequiredProductId = _requiredProduct?.ProductId; // Обязателен
                    discountToSave.GiftProductId = _giftProduct?.ProductId;         // Обязателен
                    if (int.TryParse(RequiredQuantityNTextBox.Text, out int n)) discountToSave.RequiredQuantityN = n;
                    if (int.TryParse(GiftQuantityMTextBox.Text, out int m)) discountToSave.GiftQuantityM = m;
                    break;
                case "Скидка на N-ный":
                    discountToSave.RequiredProductId = _requiredProduct?.ProductId; // Обязателен
                    if (int.TryParse(NthItemNumberTextBox.Text, out int nth)) discountToSave.NthItemNumber = nth;
                    discountToSave.IsNthDiscountPercentage = NthDiscountPercentageRadio.IsChecked == true;
                    if (decimal.TryParse(NthValueTextBox.Text, NumberStyles.Any, CultureInfo.CurrentCulture, out parsedValue))
                        discountToSave.Value = Math.Round(parsedValue, 2);
                    break;
                case "Скидка на сумму чека":
                    if (decimal.TryParse(CheckAmountThresholdTextBox.Text, NumberStyles.Any, CultureInfo.CurrentCulture, out parsedValue))
                        discountToSave.CheckAmountThreshold = Math.Round(parsedValue, 2);
                    discountToSave.IsCheckDiscountPercentage = CheckDiscountPercentageRadio.IsChecked == true;
                    if (decimal.TryParse(CheckValueTextBox.Text, NumberStyles.Any, CultureInfo.CurrentCulture, out parsedValue))
                        discountToSave.Value = Math.Round(parsedValue, 2);
                    break;
            }

            // Сохранение в БД
            try
            {
                bool success;
                if (IsEditMode) success = _discountRepository.UpdateDiscount(discountToSave);
                else success = _discountRepository.AddDiscount(discountToSave) > 0;

                if (success) this.DialogResult = true;
                else ShowError("Не удалось сохранить акцию.");
            }
            catch (Exception ex)
            {
                ShowError($"Ошибка сохранения: {ex.Message}");
                System.Diagnostics.Debug.WriteLine($"Save discount error: {ex}");
            }
        }

        // --- Метод валидации ---
        private bool ValidateInput()
        {
            string name = NameTextBox.Text.Trim();
            string type = (TypeComboBox.SelectedItem as ComboBoxItem)?.Content.ToString();
            DateTime? startDate = StartDatePicker.SelectedDate;
            DateTime? endDate = EndDatePicker.SelectedDate;

            if (string.IsNullOrWhiteSpace(name)) { ShowError("Введите название акции."); NameTextBox.Focus(); return false; }
            if (string.IsNullOrEmpty(type)) { ShowError("Выберите тип акции."); TypeComboBox.Focus(); return false; }
            if (startDate.HasValue && endDate.HasValue && endDate.Value < startDate.Value) { ShowError("Дата окончания не может быть раньше даты начала."); EndDatePicker.Focus(); return false; }

            decimal parsedValue; int parsedInt; // Для TryParse

            switch (type)
            {
                case "Процент":
                case "Сумма":
                case "Фикс. цена":
                    if (!decimal.TryParse(ValueTextBox.Text, NumberStyles.Any, CultureInfo.CurrentCulture, out parsedValue) || parsedValue <= 0)
                    { ShowError($"Введите корректное положительное значение для типа '{type}'."); ValueTextBox.Focus(); return false; }
                    if (type == "Процент" && parsedValue > 100)
                    { ShowError("Процент скидки не может быть больше 100."); ValueTextBox.Focus(); return false; }
                    // Проверка найден ли RequiredProduct, если его код введен
                    if (!string.IsNullOrWhiteSpace(RequiredProductTextBox.Text) && _requiredProduct == null)
                    { ShowError($"Товар-условие '{RequiredProductTextBox.Text}' не найден. Нажмите 'Найти'."); RequiredProductTextBox.Focus(); return false; }
                    break;
                case "Подарок":
                    // Проверка найден ли RequiredProduct, если его код введен
                    if (!string.IsNullOrWhiteSpace(RequiredProductTextBox.Text) && _requiredProduct == null)
                    { ShowError($"Товар-условие '{RequiredProductTextBox.Text}' не найден. Нажмите 'Найти'."); RequiredProductTextBox.Focus(); return false; }
                    // Проверка GiftProduct - он обязателен
                    if (_giftProduct == null)
                    { ShowError("Необходимо указать и найти товар-подарок."); GiftProductTextBox.Focus(); return false; }
                    break;
                case "N+M Подарок":
                    // RequiredProduct и GiftProduct обязательны
                    if (_requiredProduct == null) { ShowError("Необходимо указать и найти товар-условие (X)."); RequiredProductTextBox.Focus(); return false; }
                    if (_giftProduct == null) { ShowError("Необходимо указать и найти товар-подарок (Y)."); GiftProductTextBox.Focus(); return false; }
                    // N и M обязательны и должны быть > 0
                    if (!int.TryParse(RequiredQuantityNTextBox.Text, out parsedInt) || parsedInt <= 0) { ShowError("Введите корректное целое положительное количество N (купить)."); RequiredQuantityNTextBox.Focus(); return false; }
                    if (!int.TryParse(GiftQuantityMTextBox.Text, out parsedInt) || parsedInt <= 0) { ShowError("Введите корректное целое положительное количество M (подарок)."); GiftQuantityMTextBox.Focus(); return false; }
                    break;
                case "Скидка на N-ный":
                    // RequiredProduct обязателен
                    if (_requiredProduct == null) { ShowError("Необходимо указать и найти товар-условие."); RequiredProductTextBox.Focus(); return false; }
                    // N обязателен и должен быть > 0
                    if (!int.TryParse(NthItemNumberTextBox.Text, out parsedInt) || parsedInt <= 0) { ShowError("Введите корректный номер N-ного товара (N > 0)."); NthItemNumberTextBox.Focus(); return false; }
                    // Значение скидки (Value) обязательно и > 0
                    if (!decimal.TryParse(NthValueTextBox.Text, NumberStyles.Any, CultureInfo.CurrentCulture, out parsedValue) || parsedValue <= 0)
                    { ShowError("Введите корректное положительное значение скидки (процент или сумма)."); NthValueTextBox.Focus(); return false; }
                    if (NthDiscountPercentageRadio.IsChecked == true && parsedValue > 100) { ShowError("Процент скидки не может быть больше 100."); NthValueTextBox.Focus(); return false; }
                    break;
                case "Скидка на сумму чека":
                    // Порог обязателен и > 0
                    if (!decimal.TryParse(CheckAmountThresholdTextBox.Text, NumberStyles.Any, CultureInfo.CurrentCulture, out parsedValue) || parsedValue <= 0)
                    { ShowError("Введите корректный положительный порог суммы чека."); CheckAmountThresholdTextBox.Focus(); return false; }
                    // Значение скидки (Value) обязательно и > 0
                    if (!decimal.TryParse(CheckValueTextBox.Text, NumberStyles.Any, CultureInfo.CurrentCulture, out parsedValue) || parsedValue <= 0)
                    { ShowError("Введите корректное положительное значение скидки (процент или сумма)."); CheckValueTextBox.Focus(); return false; }
                    if (CheckDiscountPercentageRadio.IsChecked == true && parsedValue > 100) { ShowError("Процент скидки не может быть больше 100."); CheckValueTextBox.Focus(); return false; }
                    break;
            }

            return true; // Все проверки пройдены
        }

        // --- Вспомогательные ---
        private void ShowError(string message) { ErrorText.Text = message; ErrorText.Visibility = Visibility.Visible; }
        private void ClearError() { ErrorText.Text = string.Empty; ErrorText.Visibility = Visibility.Collapsed; }
    }
}