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
    /// Логика взаимодействия для AddEditProductWindow.xaml
    /// </summary>
    public partial class AddEditProductWindow : Window
    {
        private readonly ProductRepository _productRepository;
        private readonly Product _originalProduct; // Хранит продукт для редактирования (null если добавление)
        private bool IsEditMode => _originalProduct != null;

        // Конструктор для добавления нового товара
        public AddEditProductWindow()
        {
            InitializeComponent();
            _productRepository = new ProductRepository();
            _originalProduct = null; // Режим добавления
            this.Title = "Добавление товара";
        }

        // Конструктор для редактирования существующего товара
        public AddEditProductWindow(Product productToEdit) : this() // Вызываем основной конструктор
        {
            _originalProduct = productToEdit;
            this.Title = "Редактирование товара";
            // Заполняем поля данными редактируемого товара
            ProductCodeTextBox.Text = _originalProduct.ProductCode;
            NameTextBox.Text = _originalProduct.Name;
            DescriptionTextBox.Text = _originalProduct.Description;
            BarcodeTextBox.Text = _originalProduct.Barcode;
            PriceTextBox.Text = _originalProduct.Price.ToString(CultureInfo.CurrentCulture); // Форматируем цену
        }

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            // Фокус на первое обязательное поле
            ProductCodeTextBox.Focus();
        }

        // Валидация для поля Цены (как в PaymentDialog)
        private void PriceTextBox_PreviewTextInput(object sender, TextCompositionEventArgs e)
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

        // Кнопка Сохранить
        private void SaveButton_Click(object sender, RoutedEventArgs e)
        {
            ClearError();

            // 1. Валидация ввода
            string productCode = ProductCodeTextBox.Text.Trim();
            string name = NameTextBox.Text.Trim();
            string description = DescriptionTextBox.Text.Trim();
            string barcode = BarcodeTextBox.Text.Trim();
            string priceString = PriceTextBox.Text.Trim();

            if (string.IsNullOrWhiteSpace(productCode))
            {
                ShowError("Код (САП) не может быть пустым.");
                ProductCodeTextBox.Focus();
                return;
            }
            if (string.IsNullOrWhiteSpace(name))
            {
                ShowError("Наименование не может быть пустым.");
                NameTextBox.Focus();
                return;
            }
            if (!decimal.TryParse(priceString, NumberStyles.Any, CultureInfo.CurrentCulture, out decimal price) || price < 0)
            {
                ShowError("Введите корректную неотрицательную цену.");
                PriceTextBox.Focus();
                PriceTextBox.SelectAll();
                return;
            }
            // Доп. валидация: Уникальность кода товара (кроме случая редактирования самого себя)
            var existingProduct = _productRepository.FindProductByCodeOrBarcode(productCode);
            if (existingProduct != null && (!IsEditMode || existingProduct.ProductId != _originalProduct.ProductId))
            {
                ShowError($"Товар с кодом (САП) '{productCode}' уже существует.");
                ProductCodeTextBox.Focus();
                ProductCodeTextBox.SelectAll();
                return;
            }
            // Доп. валидация: Уникальность штрих-кода (если он введен и не пуст)
            if (!string.IsNullOrEmpty(barcode))
            {
                var existingBarcode = _productRepository.FindProductByCodeOrBarcode(barcode);
                if (existingBarcode != null && (!IsEditMode || existingBarcode.ProductId != _originalProduct.ProductId))
                {
                    ShowError($"Товар со штрих-кодом '{barcode}' уже существует.");
                    BarcodeTextBox.Focus();
                    BarcodeTextBox.SelectAll();
                    return;
                }
            }


            // 2. Создание или обновление объекта Product
            Product productToSave;
            if (IsEditMode)
            {
                productToSave = _originalProduct; // Обновляем существующий
            }
            else
            {
                productToSave = new Product(); // Создаем новый
            }

            productToSave.ProductCode = productCode;
            productToSave.Name = name;
            productToSave.Description = string.IsNullOrWhiteSpace(description) ? null : description;
            productToSave.Barcode = string.IsNullOrEmpty(barcode) ? null : barcode; // Сохраняем null, если ШК пустой
            productToSave.Price = Math.Round(price, 2); // Округляем цену

            // 3. Сохранение в БД
            try
            {
                bool success;
                if (IsEditMode)
                {
                    success = _productRepository.UpdateProduct(productToSave);
                }
                else
                {
                    int newId = _productRepository.AddProduct(productToSave);
                    success = newId > 0;
                }

                if (success)
                {
                    this.DialogResult = true; // Успешно, закрываем окно
                }
                else
                {
                    ShowError("Не удалось сохранить товар. Проверьте данные или обратитесь к администратору.");
                }
            }
            catch (Exception ex) // Ловим ошибки БД (например, нарушение UNIQUE constraint, если валидация пропустила)
            {
                ShowError($"Ошибка сохранения: {ex.Message}");
                System.Diagnostics.Debug.WriteLine($"Save product error: {ex}");
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