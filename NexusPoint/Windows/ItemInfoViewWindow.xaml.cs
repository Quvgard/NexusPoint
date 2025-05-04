using NexusPoint.Data.Repositories;
using NexusPoint.Models;
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
    /// <summary>
    /// Логика взаимодействия для ItemInfoViewWindow.xaml
    /// </summary>
    public partial class ItemInfoViewWindow : Window
    {
        private readonly ProductRepository _productRepository;
        private readonly StockItemRepository _stockItemRepository;

        public ItemInfoViewWindow()
        {
            InitializeComponent();
            _productRepository = new ProductRepository();
            _stockItemRepository = new StockItemRepository();
        }

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            ItemInputTextBox.Focus();
        }

        // Нажатие Enter в поле ввода
        private void ItemInputTextBox_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter && !string.IsNullOrWhiteSpace(ItemInputTextBox.Text))
            {
                FindItem();
                e.Handled = true; // Поглощаем Enter
            }
        }

        // Нажатие кнопки "Найти"
        private void FindItemButton_Click(object sender, RoutedEventArgs e)
        {
            FindItem();
        }

        // Основная логика поиска и отображения
        private void FindItem()
        {
            ClearError();
            ClearItemInfo();

            string codeOrBarcode = ItemInputTextBox.Text.Trim();
            if (string.IsNullOrWhiteSpace(codeOrBarcode))
            {
                ShowError("Введите код или штрих-код товара.");
                return;
            }

            try
            {
                // Ищем товар в каталоге
                Product product = _productRepository.FindProductByCodeOrBarcode(codeOrBarcode);

                if (product == null)
                {
                    ShowError($"Товар с кодом/ШК '{codeOrBarcode}' не найден.");
                    return;
                }

                // Товар найден, получаем остаток
                decimal stockQuantity = _stockItemRepository.GetStockQuantity(product.ProductId);

                // Отображаем информацию
                DisplayItemInfo(product, stockQuantity);

            }
            catch (Exception ex)
            {
                ShowError($"Ошибка при поиске товара: {ex.Message}");
                System.Diagnostics.Debug.WriteLine($"Find item error: {ex}");
            }
        }

        // Отображение информации о найденном товаре
        private void DisplayItemInfo(Product product, decimal stockQuantity)
        {
            CultureInfo culture = CultureInfo.CurrentCulture; // Или GetCultureInfo("ru-RU")

            ItemNameText.Text = product.Name;
            ItemDescriptionText.Text = product.Description ?? "-";
            ItemCodeText.Text = product.ProductCode;
            ItemBarcodeText.Text = product.Barcode ?? "-"; // Показываем прочерк, если ШК нет
            ItemPriceText.Text = product.Price.ToString("C", culture);
            ItemStockText.Text = stockQuantity.ToString("N", culture); // "N" - числовой формат с разделителями

            ItemInfoGrid.Visibility = Visibility.Visible; // Показываем панель с информацией
            ItemInputTextBox.Focus(); // Возвращаем фокус на ввод для следующего поиска
            ItemInputTextBox.SelectAll(); // Выделяем текст
        }

        // Очистка отображаемой информации
        private void ClearItemInfo()
        {
            ItemInfoGrid.Visibility = Visibility.Collapsed; // Скрываем панель
            ItemNameText.Text = string.Empty;
            ItemDescriptionText.Text = string.Empty;
            ItemCodeText.Text = string.Empty;
            ItemBarcodeText.Text = string.Empty;
            ItemPriceText.Text = string.Empty;
            ItemStockText.Text = string.Empty;
        }

        // Показ/Скрытие ошибки
        private void ShowError(string message)
        {
            StatusText.Text = message;
        }

        private void ClearError()
        {
            StatusText.Text = string.Empty;
        }
    }
}