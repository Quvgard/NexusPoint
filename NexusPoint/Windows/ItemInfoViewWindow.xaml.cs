using NexusPoint.BusinessLogic;
using NexusPoint.Models;
using System;
using System.Globalization;
using System.Windows;
using System.Windows.Input;

namespace NexusPoint.Windows
{
    public partial class ItemInfoViewWindow : Window
    {
        private readonly ProductManager _productManager;
        private readonly StockManager _stockManager;
        private CultureInfo _culture = CultureInfo.CurrentCulture;
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
        private void ItemInputTextBox_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter && !string.IsNullOrWhiteSpace(ItemInputTextBox.Text))
            {
                FindItem();
                e.Handled = true;
            }
        }
        private void FindItemButton_Click(object sender, RoutedEventArgs e)
        {
            FindItem();
        }
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
                Product product = _productManager.FindByCodeOrBarcodeInternal(codeOrBarcode);

                if (product == null)
                {
                    ShowError($"Товар с кодом/ШК '{codeOrBarcode}' не найден.");
                    ItemInputTextBox.Focus(); ItemInputTextBox.SelectAll();
                    return;
                }
                decimal stockQuantity = _stockManager.GetStockQuantityByProductId(product.ProductId);
                DisplayItemInfo(product, stockQuantity);

            }
            catch (Exception ex)
            {
                ShowError($"Ошибка при поиске товара: {ex.Message}");
                System.Diagnostics.Debug.WriteLine($"Find item error: {ex}");
            }
        }
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
        private void ClearItemInfo()
        {
            ItemInfoGrid.Visibility = Visibility.Collapsed;
            ItemNameText.Text = string.Empty; ItemDescriptionText.Text = string.Empty;
            ItemCodeText.Text = string.Empty; ItemBarcodeText.Text = string.Empty;
            ItemPriceText.Text = string.Empty; ItemStockText.Text = string.Empty;
        }
        private void ShowError(string message) { StatusText.Text = message; }
        private void ClearError() { StatusText.Text = string.Empty; }
    }
}