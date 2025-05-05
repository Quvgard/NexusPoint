using NexusPoint.BusinessLogic;
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
    public partial class ItemInfoViewWindow : Window
    {
        // Заменяем репозитории на менеджеры
        private readonly ProductManager _productManager;
        private readonly StockManager _stockManager;
        private CultureInfo _culture = CultureInfo.CurrentCulture; // Для форматирования

        // Конструктор принимает менеджеры
        public ItemInfoViewWindow(ProductManager productManager, StockManager stockManager)
        {
            InitializeComponent();
            _productManager = productManager ?? throw new ArgumentNullException(nameof(productManager));
            _stockManager = stockManager ?? throw new ArgumentNullException(nameof(stockManager));
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
                e.Handled = true;
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
                // Ищем товар через ProductManager
                Product product = _productManager.FindByCodeOrBarcodeInternal(codeOrBarcode);

                if (product == null)
                {
                    ShowError($"Товар с кодом/ШК '{codeOrBarcode}' не найден.");
                    ItemInputTextBox.Focus(); ItemInputTextBox.SelectAll(); // Возвращаем фокус для исправления
                    return;
                }

                // Товар найден, получаем остаток через StockManager
                decimal stockQuantity = _stockManager.GetStockQuantityByProductId(product.ProductId);
                // Ошибки получения остатка обработаются в StockManager

                // Отображаем информацию
                DisplayItemInfo(product, stockQuantity);

            }
            catch (Exception ex) // Ловим ошибки, которые могли возникнуть не в менеджерах
            {
                ShowError($"Ошибка при поиске товара: {ex.Message}");
                System.Diagnostics.Debug.WriteLine($"Find item error: {ex}");
            }
        }

        // Отображение информации о найденном товаре 
        private void DisplayItemInfo(Product product, decimal stockQuantity)
        {
            ItemNameText.Text = product.Name;
            ItemDescriptionText.Text = product.Description ?? "-";
            ItemCodeText.Text = product.ProductCode;
            ItemBarcodeText.Text = product.Barcode ?? "-";
            ItemPriceText.Text = product.Price.ToString("C", _culture);
            ItemStockText.Text = stockQuantity.ToString("N", _culture);

            ItemInfoGrid.Visibility = Visibility.Visible;
            ItemInputTextBox.Focus();
            ItemInputTextBox.SelectAll();
        }

        // Очистка отображаемой информации 
        private void ClearItemInfo()
        {
            ItemInfoGrid.Visibility = Visibility.Collapsed;
            ItemNameText.Text = string.Empty; ItemDescriptionText.Text = string.Empty;
            ItemCodeText.Text = string.Empty; ItemBarcodeText.Text = string.Empty;
            ItemPriceText.Text = string.Empty; ItemStockText.Text = string.Empty;
        }

        // Показ/Скрытие ошибки 
        private void ShowError(string message) { StatusText.Text = message; }
        private void ClearError() { StatusText.Text = string.Empty; }
    }
}