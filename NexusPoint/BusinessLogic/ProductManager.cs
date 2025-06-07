using NexusPoint.Data.Repositories;
using NexusPoint.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;

namespace NexusPoint.BusinessLogic
{
    public class ProductManager
    {
        private readonly ProductRepository _productRepository;

        public ProductManager(ProductRepository productRepository)
        {
            _productRepository = productRepository ?? throw new ArgumentNullException(nameof(productRepository));
        }
        public async Task<IEnumerable<Product>> GetProductsAsync(string searchTerm = null)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(searchTerm))
                {
                    return await Task.Run(() => _productRepository.GetAllProducts());
                }
                else
                {
                    return await Task.Run(() => _productRepository.SearchProductsByName(searchTerm));
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка загрузки списка товаров: {ex.Message}", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
                return new List<Product>();
            }
        }

        public async Task<IEnumerable<Product>> GetProductsByIdsAsync(List<int> productIds)
        {
            if (productIds == null || !productIds.Any()) return new List<Product>();
            try
            {
                return await Task.Run(() => _productRepository.GetProductsByIds(productIds));
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка загрузки товаров по ID: {ex.Message}", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
                return new List<Product>();
            }
        }
        public Product FindByCodeOrBarcodeInternal(string identifier)
        {
            if (string.IsNullOrEmpty(identifier)) return null;
            try
            {
                return _productRepository.FindProductByCodeOrBarcode(identifier);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Internal check failed for identifier '{identifier}': {ex.Message}");
                return null;
            }
        }
        public bool AddProduct(Product product)
        {
            if (product == null) return false;
            var existingByCode = FindByCodeOrBarcodeInternal(product.ProductCode);
            if (existingByCode != null)
            {
                MessageBox.Show($"Товар с кодом (САП) '{product.ProductCode}' уже существует.", "Ошибка валидации", MessageBoxButton.OK, MessageBoxImage.Warning);
                return false;
            }
            if (!string.IsNullOrEmpty(product.Barcode))
            {
                var existingByBarcode = FindByCodeOrBarcodeInternal(product.Barcode);
                if (existingByBarcode != null)
                {
                    MessageBox.Show($"Товар со штрих-кодом '{product.Barcode}' уже существует.", "Ошибка валидации", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return false;
                }
            }

            try
            {
                int newId = _productRepository.AddProduct(product);
                return newId > 0;
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка добавления товара: {ex.Message}", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
                return false;
            }
        }

        public bool UpdateProduct(Product product)
        {
            if (product == null) return false;
            var existingByCode = FindByCodeOrBarcodeInternal(product.ProductCode);
            if (existingByCode != null && existingByCode.ProductId != product.ProductId)
            {
                MessageBox.Show($"Товар с кодом (САП) '{product.ProductCode}' уже существует (другой товар).", "Ошибка валидации", MessageBoxButton.OK, MessageBoxImage.Warning);
                return false;
            }
            if (!string.IsNullOrEmpty(product.Barcode))
            {
                var existingByBarcode = FindByCodeOrBarcodeInternal(product.Barcode);
                if (existingByBarcode != null && existingByBarcode.ProductId != product.ProductId)
                {
                    MessageBox.Show($"Товар со штрих-кодом '{product.Barcode}' уже существует (другой товар).", "Ошибка валидации", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return false;
                }
            }

            try
            {
                return _productRepository.UpdateProduct(product);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка обновления товара: {ex.Message}", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
                return false;
            }
        }

        public bool DeleteProduct(int productId)
        {
            try
            {
                return _productRepository.DeleteProduct(productId);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка при удалении товара: {ex.Message}", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
                return false;
            }
        }
    }
}