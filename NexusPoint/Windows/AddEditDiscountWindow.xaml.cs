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
        private readonly DiscountRepository _discountRepository;
        private readonly ProductRepository _productRepository;
        private readonly Discount _originalDiscount; // null для добавления
        private bool IsEditMode => _originalDiscount != null;

        private Product _requiredProduct = null; // Найденный товар-условие
        private Product _giftProduct = null;     // Найденный товар-подарок

        // Конструктор для добавления
        public AddEditDiscountWindow()
        {
            InitializeComponent();
            _discountRepository = new DiscountRepository();
            _productRepository = new ProductRepository();
            this.Title = "Добавление акции";
        }

        // Конструктор для редактирования
        public AddEditDiscountWindow(Discount discountToEdit) : this()
        {
            _originalDiscount = discountToEdit;
            this.Title = "Редактирование акции";
            LoadDiscountData(); // Загружаем данные в поля
        }

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            // Устанавливаем тип скидки по умолчанию при добавлении
            if (!IsEditMode)
            {
                TypeComboBox.SelectedIndex = 0; // Percentage
            }
            UpdateUIBasedOnType(); // Обновляем видимость/активность полей
            NameTextBox.Focus();
        }

        // Загрузка данных скидки в поля (для режима редактирования)
        private async void LoadDiscountData()
        {
            if (_originalDiscount == null) return;

            NameTextBox.Text = _originalDiscount.Name;

            // Выбираем тип
            foreach (ComboBoxItem item in TypeComboBox.Items)
            {
                if (item.Content.ToString() == _originalDiscount.Type)
                {
                    TypeComboBox.SelectedItem = item;
                    break;
                }
            }

            ValueTextBox.Text = _originalDiscount.Value?.ToString(CultureInfo.CurrentCulture) ?? "";

            // Загружаем товары (если ID указаны)
            if (_originalDiscount.RequiredProductId.HasValue)
            {
                _requiredProduct = await System.Threading.Tasks.Task.Run(() => // Асинхронно в фоне
                   _productRepository.FindProductById(_originalDiscount.RequiredProductId.Value));
                RequiredProductTextBox.Text = _requiredProduct?.ProductCode ?? _originalDiscount.RequiredProductId.ToString(); // Показываем код или ID
                RequiredProductNameText.Text = _requiredProduct?.Name ?? "<не найден>";
            }
            if (_originalDiscount.GiftProductId.HasValue)
            {
                _giftProduct = await System.Threading.Tasks.Task.Run(() =>
                   _productRepository.FindProductById(_originalDiscount.GiftProductId.Value));
                GiftProductTextBox.Text = _giftProduct?.ProductCode ?? _originalDiscount.GiftProductId.ToString();
                GiftProductNameText.Text = _giftProduct?.Name ?? "<не найден>";
            }


            StartDatePicker.SelectedDate = _originalDiscount.StartDate;
            EndDatePicker.SelectedDate = _originalDiscount.EndDate;
            IsActiveCheckBox.IsChecked = _originalDiscount.IsActive;

            UpdateUIBasedOnType(); // Обновляем UI после загрузки
        }


        // Обновление UI в зависимости от типа скидки
        private void TypeComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            UpdateUIBasedOnType();
        }

        private void UpdateUIBasedOnType()
        {
            if (ValueTextBox == null || GiftProductTextBox == null || FindGiftProductButton == null) return;

            string selectedType = (TypeComboBox.SelectedItem as ComboBoxItem)?.Content.ToString();

            // Поле Значение (Value) - теперь активно и для "Фикс. цена"
            bool isValueEnabled = selectedType == "Процент" || selectedType == "Сумма" || selectedType == "Фикс. цена";
            ValueTextBox.IsEnabled = isValueEnabled;

            // Определяем суффикс для поля Значение
            if (selectedType == "Процент")
                ValueSuffixText.Text = "%";
            else if (selectedType == "Сумма" || selectedType == "Фикс. цена")
                ValueSuffixText.Text = CultureInfo.CurrentCulture.NumberFormat.CurrencySymbol; // Валюта
            else
                ValueSuffixText.Text = ""; // Для Подарка суффикса нет

            if (!isValueEnabled) ValueTextBox.Clear();

            // Поле Товар-подарок
            bool isGiftEnabled = selectedType == "Подарок";
            GiftProductTextBox.IsEnabled = isGiftEnabled;
            FindGiftProductButton.IsEnabled = isGiftEnabled;
            if (!isGiftEnabled)
            {
                GiftProductTextBox.Clear();
                GiftProductNameText.Text = "";
                _giftProduct = null;
            }

            // Поле Товар-условие теперь не нужно для типа "Подарок", если подарок НЕ зависит от покупки другого товара
            // Но если акция "Купи X - получи Y в подарок", то RequiredProduct нужен.
            // Оставим его пока активным всегда, кроме типа Подарок? Или сделать зависимым от логики?
            // Пока оставляем как есть - RequiredProduct можно указать для любого типа.
        }


        // Валидация ввода значения скидки
        private void ValueTextBox_PreviewTextInput(object sender, TextCompositionEventArgs e)
        {
            // Такая же валидация, как для цены/количества
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

        // --- Поиск товаров (упрощенный вариант по коду/ШК) ---
        private async void FindRequiredProductButton_Click(object sender, RoutedEventArgs e)
        {
            string codeOrBarcode = RequiredProductTextBox.Text.Trim();
            if (string.IsNullOrEmpty(codeOrBarcode)) return;

            ClearError();
            RequiredProductNameText.Text = "Поиск...";
            _requiredProduct = null; // Сбрасываем предыдущий

            try
            {
                _requiredProduct = await System.Threading.Tasks.Task.Run(() =>
                    _productRepository.FindProductByCodeOrBarcode(codeOrBarcode));

                if (_requiredProduct != null)
                {
                    RequiredProductNameText.Text = _requiredProduct.Name;
                    RequiredProductTextBox.Text = _requiredProduct.ProductCode; // Ставим код для единообразия
                }
                else
                {
                    RequiredProductNameText.Text = "<не найден>";
                    ShowError($"Товар (условие) с кодом/ШК '{codeOrBarcode}' не найден.");
                }
            }
            catch (Exception ex) { RequiredProductNameText.Text = "<ошибка>"; ShowError($"Ошибка поиска: {ex.Message}"); }
        }

        private async void FindGiftProductButton_Click(object sender, RoutedEventArgs e)
        {
            string codeOrBarcode = GiftProductTextBox.Text.Trim();
            if (string.IsNullOrEmpty(codeOrBarcode)) return;

            ClearError();
            GiftProductNameText.Text = "Поиск...";
            _giftProduct = null;

            try
            {
                _giftProduct = await System.Threading.Tasks.Task.Run(() =>
                    _productRepository.FindProductByCodeOrBarcode(codeOrBarcode));

                if (_giftProduct != null)
                {
                    GiftProductNameText.Text = _giftProduct.Name;
                    GiftProductTextBox.Text = _giftProduct.ProductCode;
                }
                else
                {
                    GiftProductNameText.Text = "<не найден>";
                    ShowError($"Товар (подарок) с кодом/ШК '{codeOrBarcode}' не найден.");
                }
            }
            catch (Exception ex) { GiftProductNameText.Text = "<ошибка>"; ShowError($"Ошибка поиска: {ex.Message}"); }
        }


        // --- Сохранение ---
        private void SaveButton_Click(object sender, RoutedEventArgs e)
        {
            ClearError();

            // 1. Собираем данные
            string name = NameTextBox.Text.Trim();
            string type = (TypeComboBox.SelectedItem as ComboBoxItem)?.Content.ToString();
            string valueString = ValueTextBox.Text.Trim();
            string requiredProductInput = RequiredProductTextBox.Text.Trim(); // Это может быть ID или Code
            string giftProductInput = GiftProductTextBox.Text.Trim();
            DateTime? startDate = StartDatePicker.SelectedDate;
            DateTime? endDate = EndDatePicker.SelectedDate;
            bool isActive = IsActiveCheckBox.IsChecked ?? false;

            // 2. Валидация
            if (string.IsNullOrWhiteSpace(name)) { ShowError("Введите название акции."); NameTextBox.Focus(); return; }
            if (string.IsNullOrEmpty(type)) { ShowError("Выберите тип акции."); TypeComboBox.Focus(); return; }

            decimal? value = null;
            // Проверяем значение, если тип требует его
            if (type == "Процент" || type == "Сумма" || type == "Фикс. цена")
            {
                if (!decimal.TryParse(valueString, NumberStyles.Any, CultureInfo.CurrentCulture, out decimal parsedValue) || parsedValue <= 0)
                { ShowError($"Введите корректное положительное значение для типа '{type}'."); ValueTextBox.Focus(); return; }

                if (type == "Процент" && parsedValue > 100)
                { ShowError("Процент скидки не может быть больше 100."); ValueTextBox.Focus(); return; }

                // Для Фикс. цены тоже округляем до копеек
                value = Math.Round(parsedValue, 2);
            }

            // Проверка подарка
            int? giftProductId = null; // Объявляем заранее
            if (type == "Подарок")
            {
                if (_giftProduct == null)
                {
                    if (string.IsNullOrWhiteSpace(giftProductInput)) { ShowError("Для акции типа 'Подарок' необходимо указать товар-подарок."); GiftProductTextBox.Focus(); return; }
                    else { ShowError($"Товар-подарок '{giftProductInput}' не найден или не выбран. Нажмите 'Найти' для поиска."); GiftProductTextBox.Focus(); return; }
                }
                giftProductId = _giftProduct.ProductId; // Присваиваем ID найденного подарка
            }

            // Валидация товара-условия (остается как было)
            int? requiredProductId = null;
            if (!string.IsNullOrWhiteSpace(requiredProductInput))
            {
                if (_requiredProduct == null || (_requiredProduct.ProductCode != requiredProductInput && _requiredProduct.Barcode != requiredProductInput)) // Уточняем проверку
                {
                    ShowError($"Товар-условие '{requiredProductInput}' не найден или не выбран. Нажмите 'Найти' для поиска."); RequiredProductTextBox.Focus(); return;
                }
                requiredProductId = _requiredProduct.ProductId;
            }

            if (startDate.HasValue && endDate.HasValue && endDate.Value < startDate.Value)
            { ShowError("Дата окончания не может быть раньше даты начала."); EndDatePicker.Focus(); return; }


            // 3. Создание/обновление объекта Discount
            Discount discountToSave;
            if (IsEditMode)
            {
                discountToSave = _originalDiscount;
            }
            else
            {
                discountToSave = new Discount();
            }

            discountToSave.Name = name;
            discountToSave.Type = type;
            discountToSave.Value = value; // Может быть null для Gift
            discountToSave.RequiredProductId = requiredProductId;
            discountToSave.GiftProductId = giftProductId; // Может быть null для не-Gift
            discountToSave.StartDate = startDate;
            discountToSave.EndDate = endDate;
            discountToSave.IsActive = isActive;


            // 4. Сохранение в БД
            try
            {
                bool success;
                if (IsEditMode)
                {
                    success = _discountRepository.UpdateDiscount(discountToSave);
                }
                else
                {
                    int newId = _discountRepository.AddDiscount(discountToSave);
                    success = newId > 0;
                }

                if (success)
                {
                    this.DialogResult = true; // Успех
                }
                else
                {
                    ShowError("Не удалось сохранить акцию.");
                }
            }
            catch (Exception ex)
            {
                ShowError($"Ошибка сохранения: {ex.Message}");
                System.Diagnostics.Debug.WriteLine($"Save discount error: {ex}");
            }
        }

        // --- Вспомогательные ---
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