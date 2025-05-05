using NexusPoint.Data.Repositories;
using NexusPoint.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;

namespace NexusPoint.BusinessLogic
{
    // Модель для отображения остатков с деталями товара (переносим сюда из ManagerWindow)
    public class StockItemView // Не наследуем INPC, т.к. ManagerWindow не требует динамического обновления строк
    {
        public int StockItemId { get; set; }
        public int ProductId { get; set; }
        public string ProductCode { get; set; } = "...";
        public string Barcode { get; set; } = "...";
        public string ProductName { get; set; } = "Загрузка...";
        public decimal Quantity { get; set; }
        public DateTime LastUpdated { get; set; }
    }

    public class StockManager
    {
        private readonly StockItemRepository _stockItemRepository;
        private readonly ProductRepository _productRepository; // Нужен для получения деталей

        public StockManager(StockItemRepository stockItemRepository, ProductRepository productRepository)
        {
            _stockItemRepository = stockItemRepository ?? throw new ArgumentNullException(nameof(stockItemRepository));
            _productRepository = productRepository ?? throw new ArgumentNullException(nameof(productRepository));
        }

        public decimal GetStockQuantityByProductId(int productId)
        {
            try
            {
                // Прямой вызов репозитория, т.к. дополнительной логики здесь нет
                return _stockItemRepository.GetStockQuantity(productId);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка получения остатка для товара ID {productId}: {ex.Message}", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
                return 0m; // Возвращаем 0 при ошибке
            }
        }

        public async Task<List<StockItemView>> GetStockItemsViewAsync(string searchTerm = null)
        {
            try
            {
                var stockItems = await Task.Run(() => _stockItemRepository.GetAllStockItems());
                var stockItemViews = stockItems.Select(si => new StockItemView
                {
                    StockItemId = si.StockItemId,
                    ProductId = si.ProductId,
                    Quantity = si.Quantity,
                    LastUpdated = si.LastUpdated
                }).ToList();

                // Асинхронно подгружаем детали товаров
                if (stockItemViews.Any())
                {
                    await LoadProductDetailsForStockViewAsync(stockItemViews);
                }

                // Фильтрация (после загрузки деталей для поиска по имени/коду/штрихкоду)
                if (!string.IsNullOrWhiteSpace(searchTerm))
                {
                    string lowerSearchTerm = searchTerm.ToLowerInvariant();
                    stockItemViews = stockItemViews.Where(sv =>
                        sv.ProductId.ToString() == searchTerm || // По ID
                        sv.ProductCode?.ToLowerInvariant().Contains(lowerSearchTerm) == true ||
                        sv.Barcode?.ToLowerInvariant().Contains(lowerSearchTerm) == true ||
                        sv.ProductName?.ToLowerInvariant().Contains(lowerSearchTerm) == true
                    ).ToList();
                }


                return stockItemViews;
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка загрузки остатков: {ex.Message}", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
                return new List<StockItemView>();
            }
        }

        private async Task LoadProductDetailsForStockViewAsync(List<StockItemView> stockViews)
        {
            List<int> productIds = stockViews.Select(sv => sv.ProductId).Distinct().ToList();
            if (!productIds.Any()) return;

            try
            {
                var products = (await Task.Run(() => _productRepository.GetProductsByIds(productIds)))
                               .ToDictionary(p => p.ProductId);

                foreach (var stockViewItem in stockViews)
                {
                    if (products.TryGetValue(stockViewItem.ProductId, out Product product))
                    {
                        stockViewItem.ProductCode = product.ProductCode;
                        stockViewItem.Barcode = product.Barcode;
                        stockViewItem.ProductName = product.Name;
                    }
                    else
                    {
                        stockViewItem.ProductCode = "<Н/Д>";
                        stockViewItem.Barcode = "<Н/Д>";
                        stockViewItem.ProductName = "<Товар не найден>";
                    }
                }
            }
            catch (Exception ex)
            {
                // Ошибку загрузки деталей можно не показывать пользователю, просто останутся "Загрузка..." или "<Н/Д>"
                System.Diagnostics.Debug.WriteLine($"Error loading product details for stock view: {ex.Message}");
                // Можно установить сообщение об ошибке в ProductName
                foreach (var sv in stockViews.Where(s => s.ProductName == "Загрузка..."))
                {
                    sv.ProductName = "<Ошибка загрузки>";
                    sv.ProductCode = "<Ошибка>";
                    sv.Barcode = "<Ошибка>";
                }
            }
        }

        // Методы для корректировки (можно вынести в отдельный сервис, если логика сложная)
        public bool AdjustStockQuantity(int productId, decimal quantityChange, bool isAddition)
        {
            if (quantityChange <= 0) // Количество для корректировки должно быть положительным
            {
                MessageBox.Show("Количество для приемки/списания должно быть положительным.", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Warning);
                return false;
            }

            try
            {
                // EnsureStockItemExists вызывается внутри репозитория при необходимости
                decimal change = isAddition ? quantityChange : -quantityChange;

                // Добавим проверку перед списанием
                if (!isAddition)
                {
                    decimal currentStock = _stockItemRepository.GetStockQuantity(productId);
                    if (quantityChange > currentStock)
                    {
                        MessageBox.Show($"Нельзя списать {quantityChange}. Текущий остаток: {currentStock}.", "Ошибка списания", MessageBoxButton.OK, MessageBoxImage.Warning);
                        return false;
                    }
                }

                return _stockItemRepository.UpdateStockQuantity(productId, change);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка корректировки остатка: {ex.Message}", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
                return false;
            }
        }

        public bool SetStockQuantity(int productId, decimal newQuantity)
        {
            if (newQuantity < 0)
            {
                MessageBox.Show("Устанавливаемый остаток не может быть отрицательным.", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Warning);
                return false;
            }
            try
            {
                // EnsureStockItemExists вызывается внутри репозитория при необходимости
                return _stockItemRepository.SetStockQuantity(productId, newQuantity);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка установки остатка: {ex.Message}", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
                return false;
            }
        }
    }
}
