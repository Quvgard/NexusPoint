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
    /// <summary>
    /// Логика взаимодействия для AddEditProductWindow.xaml
    /// </summary>
    public partial class AddEditProductWindow : Window
    {
        // Заменяем репозиторий на менеджер
        private readonly ProductManager _productManager;
        private readonly Product _originalProduct;
        private bool IsEditMode => _originalProduct != null;

        // Конструктор для добавления (принимает менеджер)
        public AddEditProductWindow(ProductManager productManager)
        {
            InitializeComponent();
            _productManager = productManager ?? throw new ArgumentNullException(nameof(productManager));
            _originalProduct = null;
            this.Title = "Добавление товара";
        }

        // Конструктор для редактирования (принимает менеджер и товар)
        public AddEditProductWindow(ProductManager productManager, Product productToEdit) : this(productManager) // Вызов основного конструктора
        {
            _originalProduct = productToEdit ?? throw new ArgumentNullException(nameof(productToEdit));
            this.Title = "Редактирование товара";

            // Заполнение полей из редактируемого товара
            ProductCodeTextBox.Text = _originalProduct.ProductCode;
            NameTextBox.Text = _originalProduct.Name;
            DescriptionTextBox.Text = _originalProduct.Description;
            BarcodeTextBox.Text = _originalProduct.Barcode;
            PriceTextBox.Text = _originalProduct.Price.ToString(CultureInfo.CurrentCulture);
        }

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            ProductCodeTextBox.Focus();
        }

        // Валидация ввода цены (остается без изменений)
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

        // Кнопка Сохранить (обновлено)
        private void SaveButton_Click(object sender, RoutedEventArgs e)
        {
            ClearError();

            // 1. Валидация ввода (основные проверки остаются здесь)
            string productCode = ProductCodeTextBox.Text.Trim();
            string name = NameTextBox.Text.Trim();
            string description = DescriptionTextBox.Text.Trim();
            string barcode = BarcodeTextBox.Text.Trim();
            string priceString = PriceTextBox.Text.Trim();

            if (string.IsNullOrWhiteSpace(productCode)) { ShowError("Код (САП) не может быть пустым."); ProductCodeTextBox.Focus(); return; }
            if (string.IsNullOrWhiteSpace(name)) { ShowError("Наименование не может быть пустым."); NameTextBox.Focus(); return; }
            if (!decimal.TryParse(priceString, NumberStyles.Any, CultureInfo.CurrentCulture, out decimal price) || price < 0) { ShowError("Введите корректную неотрицательную цену."); PriceTextBox.Focus(); PriceTextBox.SelectAll(); return; }

            // Проверки уникальности теперь выполняются внутри ProductManager

            // 2. Создание или обновление объекта Product
            Product productToSave;
            if (IsEditMode) { productToSave = _originalProduct; }
            else { productToSave = new Product(); }

            productToSave.ProductCode = productCode;
            productToSave.Name = name;
            productToSave.Description = string.IsNullOrWhiteSpace(description) ? null : description;
            productToSave.Barcode = string.IsNullOrEmpty(barcode) ? null : barcode;
            productToSave.Price = Math.Round(price, 2);

            // 3. Сохранение через ProductManager
            bool success;
            if (IsEditMode)
            {
                success = _productManager.UpdateProduct(productToSave);
            }
            else
            {
                success = _productManager.AddProduct(productToSave);
            }

            // 4. Закрытие окна при успехе
            if (success)
            {
                this.DialogResult = true;
            }
            // Сообщения об ошибках (включая уникальность) будут показаны менеджером
        }

        // Показ/Скрытие ошибки (остаются)
        private void ShowError(string message) { ErrorText.Text = message; ErrorText.Visibility = Visibility.Visible; }
        private void ClearError() { ErrorText.Text = string.Empty; ErrorText.Visibility = Visibility.Collapsed; }
    }
}