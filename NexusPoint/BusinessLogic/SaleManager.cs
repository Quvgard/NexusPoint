using NexusPoint.Data.Repositories;
using NexusPoint.Models;
using NexusPoint.Windows;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;
using System.Windows;

namespace NexusPoint.BusinessLogic
{
    public class SaleManager : INotifyPropertyChanged
    {
        private readonly ProductRepository _productRepository;
        private readonly StockItemRepository _stockItemRepository;

        private ObservableCollection<CheckItemView> _currentCheckItems = new ObservableCollection<CheckItemView>();
        public ReadOnlyObservableCollection<CheckItemView> CurrentCheckItems { get; }

        private decimal _subtotal = 0m;
        private decimal _totalDiscount = 0m;
        private decimal _totalAmount = 0m;
        private bool _isManualDiscountApplied = false;
        private Product _lastAddedProduct = null;


        public decimal Subtotal => _subtotal;
        public decimal TotalDiscount => _totalDiscount;
        public decimal TotalAmount => _totalAmount;
        public bool IsManualDiscountApplied => _isManualDiscountApplied;
        public Product LastAddedProduct => _lastAddedProduct;
        public bool HasItems => _currentCheckItems.Any();


        public SaleManager(ProductRepository productRepository, StockItemRepository stockItemRepository)
        {
            _productRepository = productRepository ?? throw new ArgumentNullException(nameof(productRepository));
            _stockItemRepository = stockItemRepository ?? throw new ArgumentNullException(nameof(stockItemRepository));
            CurrentCheckItems = new ReadOnlyObservableCollection<CheckItemView>(_currentCheckItems);
            _currentCheckItems.CollectionChanged += (s, e) => UpdateCheckoutButtonsState(); // Обновляем кнопки при изменении коллекции
        }

        // --- ДОБАВЛЕН ПРИВАТНЫЙ МЕТОД КОПИРОВАНИЯ ---
        private CheckItem CreateCleanCopy(CheckItemView originalView)
        {
            // Создаем базовый CheckItem из CheckItemView
            return new CheckItem
            {
                ProductId = originalView.ProductId,
                Quantity = originalView.Quantity,
                PriceAtSale = originalView.PriceAtSale,
                DiscountAmount = 0m, // Сброс
                AppliedDiscountId = null, // Сброс
                ItemTotalAmount = Math.Round(originalView.Quantity * originalView.PriceAtSale, 2) // Начальная сумма
            };
        }
        // --- КОНЕЦ ДОБАВЛЕННОГО МЕТОДА ---


        public bool AddItem(string codeOrBarcode)
        {
            if (string.IsNullOrWhiteSpace(codeOrBarcode)) return false;

            try
            {
                Product product = _productRepository.FindProductByCodeOrBarcode(codeOrBarcode);
                _lastAddedProduct = product;

                if (product == null)
                {
                    OnPropertyChanged(nameof(LastAddedProduct));
                    return false;
                }

                var existingItem = _currentCheckItems.FirstOrDefault(item => item.ProductId == product.ProductId);

                if (existingItem != null)
                {
                    // --- ИЗМЕНЕНИЕ: Вызываем PropertyChanged для Quantity ---
                    existingItem.Quantity += 1; // INotifyPropertyChanged в CheckItemView обновит строку
                    // --- КОНЕЦ ИЗМЕНЕНИЯ ---
                }
                else
                {
                    // --- ИЗМЕНЕНИЕ: УБИРАЕМ ПОДПИСКУ ---
                    // var newItem = new CheckItemView(...) // создаем как раньше
                    // newItem.PropertyChanged += ItemView_PropertyChanged; // УБИРАЕМ ЭТУ СТРОКУ
                    // --- КОНЕЦ ИЗМЕНЕНИЯ ---
                    var newItem = new CheckItemView(
                        new CheckItem
                        {
                            ProductId = product.ProductId,
                            Quantity = 1,
                            PriceAtSale = product.Price,
                            DiscountAmount = 0
                        }, product);
                    _currentCheckItems.Add(newItem);
                }

                _isManualDiscountApplied = false;
                UpdateTotals();
                OnPropertyChanged(nameof(LastAddedProduct));
                return true;
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка при добавлении товара: {ex.Message}", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
                _lastAddedProduct = null;
                OnPropertyChanged(nameof(LastAddedProduct));
                return false;
            }
        }

        public bool ChangeItemQuantity(CheckItemView itemToChange, decimal newQuantity)
        {
            if (itemToChange == null || !_currentCheckItems.Contains(itemToChange)) return false;
            if (newQuantity <= 0)
            {
                MessageBox.Show("Количество должно быть положительным.", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Warning);
                return false;
            }

            decimal stockNeeded = newQuantity - itemToChange.Quantity;
            if (stockNeeded > 0)
            {
                decimal currentStock = _stockItemRepository.GetStockQuantity(itemToChange.ProductId);
                if (stockNeeded > currentStock)
                {
                    MessageBox.Show($"Недостаточно товара '{itemToChange.ProductName}' на складе. Доступно: {currentStock}", "Ошибка остатков", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return false;
                }
            }

            // --- ИЗМЕНЕНИЕ: Просто меняем свойство, INPC сделает остальное ---
            itemToChange.Quantity = newQuantity;
            // --- КОНЕЦ ИЗМЕНЕНИЯ ---
            _isManualDiscountApplied = false;
            UpdateTotals(); // Обновляем общие итоги
            return true;
        }

        public bool StornoItem(CheckItemView itemToStorno, decimal quantityToStorno)
        {
            if (itemToStorno == null || !_currentCheckItems.Contains(itemToStorno)) return false;
            if (quantityToStorno <= 0) return false;

            // --- ИЗМЕНЕНИЕ: УБИРАЕМ ОТПИСКУ ---
            // itemToStorno.PropertyChanged -= ItemView_PropertyChanged; // УБИРАЕМ
            // --- КОНЕЦ ИЗМЕНЕНИЯ ---

            if (quantityToStorno >= itemToStorno.Quantity)
            {
                _currentCheckItems.Remove(itemToStorno);
            }
            else
            {
                // --- ИЗМЕНЕНИЕ: Просто меняем свойство ---
                itemToStorno.Quantity -= quantityToStorno;
                // --- КОНЕЦ ИЗМЕНЕНИЯ ---
            }

            _isManualDiscountApplied = false;
            UpdateTotals();
            return true;
        }

        public void ClearCheck()
        {
            // --- ИЗМЕНЕНИЕ: УБИРАЕМ ОТПИСКУ ПЕРЕД ОЧИСТКОЙ ---
            //foreach(var item in _currentCheckItems) item.PropertyChanged -= ItemView_PropertyChanged; // УБИРАЕМ
            // --- КОНЕЦ ИЗМЕНЕНИЯ ---
            _currentCheckItems.Clear();
            _isManualDiscountApplied = false;
            _lastAddedProduct = null;
            UpdateTotals();
            OnPropertyChanged(nameof(LastAddedProduct));
        }

        // Метод расчета и применения авто-скидок
        public async Task<bool> CalculateAndApplyAutoDiscountsAsync()
        {
            // --- ИЗМЕНЕНИЕ: Используем CreateCleanCopy(CheckItemView) ---
            List<CheckItem> originalItemsForCalc = _currentCheckItems
                .Select(CreateCleanCopy) // Используем новый приватный метод
                .ToList();
            // --- КОНЕЦ ИЗМЕНЕНИЯ ---

            if (!originalItemsForCalc.Any()) return true;

            DiscountCalculationResult discountResult;
            try
            {
                discountResult = await Task.Run(() => DiscountCalculator.ApplyAllAutoDiscounts(originalItemsForCalc));

                if (discountResult.GiftsToAdd.Any())
                {
                    var availableGifts = new List<CheckItem>();
                    var unavailableGiftMessages = new List<string>();
                    foreach (var gift in discountResult.GiftsToAdd)
                    {
                        decimal giftStock = await Task.Run(() => _stockItemRepository.GetStockQuantity(gift.ProductId));
                        if (giftStock >= gift.Quantity) availableGifts.Add(gift);
                        else
                        {
                            Product giftProdInfo = await Task.Run(() => _productRepository.FindProductById(gift.ProductId));
                            unavailableGiftMessages.Add($"Недостаточно подарка '{giftProdInfo?.Name ?? "ID:" + gift.ProductId}' (ост: {giftStock}). Акция не применена.");
                        }
                    }
                    discountResult.GiftsToAdd = availableGifts;
                    if (unavailableGiftMessages.Any()) MessageBox.Show(string.Join("\n", unavailableGiftMessages), "Недостаточно подарков", MessageBoxButton.OK, MessageBoxImage.Warning);
                }

                ApplyDiscountResultInternal(discountResult);
                return true;
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка применения автоматических скидок:\n{ex.Message}", "Ошибка скидок", MessageBoxButton.OK, MessageBoxImage.Warning);
                return false;
            }
        }

        // Приватный метод применения результата скидок
        private void ApplyDiscountResultInternal(DiscountCalculationResult discountResult)
        {
            // --- ИЗМЕНЕНИЕ: УБИРАЕМ ОТПИСКУ ---
            // foreach(var item in _currentCheckItems) item.PropertyChanged -= ItemView_PropertyChanged; // УБИРАЕМ
            // --- КОНЕЦ ИЗМЕНЕНИЯ ---
            _currentCheckItems.Clear();
            _isManualDiscountApplied = false;
            _lastAddedProduct = null;

            if (discountResult == null) { UpdateTotals(); return; }

            var productCache = new Dictionary<int, Product>();
            var allItemsFromCalc = discountResult.DiscountedItems.Concat(discountResult.GiftsToAdd);

            foreach (var item in allItemsFromCalc)
            {
                Product product = null;
                if (!productCache.TryGetValue(item.ProductId, out product))
                {
                    product = _productRepository.FindProductById(item.ProductId);
                    productCache[item.ProductId] = product;
                }
                var viewItem = new CheckItemView(item, product);
                // --- ИЗМЕНЕНИЕ: УБИРАЕМ ПОДПИСКУ ---
                // viewItem.PropertyChanged += ItemView_PropertyChanged; // УБИРАЕМ
                // --- КОНЕЦ ИЗМЕНЕНИЯ ---
                _currentCheckItems.Add(viewItem);
            }

            UpdateTotals();
            OnPropertyChanged(nameof(LastAddedProduct));
        }

        // Публичный метод ApplyDiscountResult
        public void ApplyDiscountResult(DiscountCalculationResult discountResult) => ApplyDiscountResultInternal(discountResult);


        // Применение ручной скидки
        public bool ApplyManualDiscount(decimal discountValue, bool isPercentage)
        {
            if (!_currentCheckItems.Any()) return false;

            // --- ИЗМЕНЕНИЕ: Используем CreateCleanCopy(CheckItemView) ---
            List<CheckItem> itemsForManualDiscount = _currentCheckItems
                .Select(CreateCleanCopy) // Используем новый приватный метод
                .ToList();
            // --- КОНЕЦ ИЗМЕНЕНИЯ ---

            decimal appliedAmount = DiscountCalculator.ApplyManualCheckDiscount(
                itemsForManualDiscount, discountValue, isPercentage);

            if (appliedAmount >= 0) // Применяем даже если скидка 0, чтобы сбросить авто-скидки
            {
                // Обновляем CurrentCheckItems
                // --- ИЗМЕНЕНИЕ: УБИРАЕМ ОТПИСКУ/ПОДПИСКУ ---
                // foreach(var item in _currentCheckItems) item.PropertyChanged -= ItemView_PropertyChanged;
                for (int i = 0; i < _currentCheckItems.Count; i++)
                {
                    // Считаем, что порядок совпадает
                    if (i < itemsForManualDiscount.Count)
                    {
                        // Напрямую обновляем свойства view model из рассчитанной модели
                        _currentCheckItems[i].DiscountAmount = itemsForManualDiscount[i].DiscountAmount;
                        _currentCheckItems[i].AppliedDiscountId = itemsForManualDiscount[i].AppliedDiscountId; // Должен быть null
                    }
                }
                // foreach(var item in _currentCheckItems) item.PropertyChanged += ItemView_PropertyChanged;
                // --- КОНЕЦ ИЗМЕНЕНИЯ ---

                _isManualDiscountApplied = (appliedAmount > 0); // Считаем ручной, только если скидка > 0
                UpdateTotals();
                return true;
            }
            return false;
        }

        // Обновление кнопок (можно сделать публичным или вызываться из CashierWindow)
        internal void UpdateCheckoutButtonsState()
        {
            // Здесь могла бы быть логика обновления IsEnabled для кнопок,
            // но она пока осталась в CashierWindow.xaml.cs
            // Вызываем PropertyChanged для HasItems, чтобы кнопки в UI могли обновиться
            OnPropertyChanged(nameof(HasItems));
        }


        private void UpdateTotals()
        {
            _subtotal = _currentCheckItems.Sum(item => item.Quantity * item.PriceAtSale);
            _totalDiscount = _currentCheckItems.Sum(item => item.DiscountAmount);
            _totalAmount = _subtotal - _totalDiscount;

            _subtotal = Math.Round(_subtotal, 2);
            _totalDiscount = Math.Round(_totalDiscount, 2);
            _totalAmount = Math.Round(_totalAmount, 2);

            OnPropertyChanged(nameof(Subtotal));
            OnPropertyChanged(nameof(TotalDiscount));
            OnPropertyChanged(nameof(TotalAmount));
            OnPropertyChanged(nameof(HasItems));
            OnPropertyChanged(nameof(IsManualDiscountApplied));
        }

        public event PropertyChangedEventHandler PropertyChanged;
        protected virtual void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}