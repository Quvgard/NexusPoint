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
    public class ProductManager
    {
        private readonly ProductRepository _productRepository;

        public ProductManager(ProductRepository productRepository)
        {
            _productRepository = productRepository ?? throw new ArgumentNullException(nameof(productRepository));
        }

        // --- Получение данных (Асинхронное) ---
        public async Task<IEnumerable<Product>> GetProductsAsync(string searchTerm = null)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(searchTerm))
                {
                    // Оборачиваем синхронный вызов в Task.Run для асинхронности
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
                // Оборачиваем синхронный вызов в Task.Run
                return await Task.Run(() => _productRepository.GetProductsByIds(productIds));
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка загрузки товаров по ID: {ex.Message}", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
                return new List<Product>();
            }
        }

        // --- Внутренний метод для проверки уникальности без MessageBox ---
        public Product FindByCodeOrBarcodeInternal(string identifier)
        {
            if (string.IsNullOrEmpty(identifier)) return null;
            try
            {
                // Используем существующий метод репозитория
                return _productRepository.FindProductByCodeOrBarcode(identifier);
            }
            catch (Exception ex)
            {
                // Логируем ошибку, если нужно, но не показываем пользователю при внутренней проверке
                System.Diagnostics.Debug.WriteLine($"Internal check failed for identifier '{identifier}': {ex.Message}");
                return null; // Считаем, что проверка не удалась (хотя товар может и существовать)
            }
        }


        // --- Операции CRUD (с валидацией уникальности) ---
        public bool AddProduct(Product product)
        {
            if (product == null) return false; // Базовая проверка

            // Проверка уникальности кода
            var existingByCode = FindByCodeOrBarcodeInternal(product.ProductCode);
            if (existingByCode != null)
            {
                MessageBox.Show($"Товар с кодом (САП) '{product.ProductCode}' уже существует.", "Ошибка валидации", MessageBoxButton.OK, MessageBoxImage.Warning);
                return false;
            }
            // Проверка уникальности штрих-кода (если он есть)
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

            // Проверка уникальности кода (кроме себя)
            var existingByCode = FindByCodeOrBarcodeInternal(product.ProductCode);
            if (existingByCode != null && existingByCode.ProductId != product.ProductId)
            {
                MessageBox.Show($"Товар с кодом (САП) '{product.ProductCode}' уже существует (другой товар).", "Ошибка валидации", MessageBoxButton.OK, MessageBoxImage.Warning);
                return false;
            }
            // Проверка уникальности штрих-кода (если он есть и кроме себя)
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
            // Подтверждение удаления должно быть показано в UI (ManagerWindow)
            try
            {
                return _productRepository.DeleteProduct(productId);
            }
            catch (Exception ex)
            {
                // Обработка ошибок FK (если товар используется в чеках и т.д.)
                MessageBox.Show($"Ошибка при удалении товара: {ex.Message}", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
                return false;
            }
        }
    }
}