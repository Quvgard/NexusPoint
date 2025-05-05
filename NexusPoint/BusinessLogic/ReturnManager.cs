using NexusPoint.Data.Repositories;
using NexusPoint.Models;
using NexusPoint.Utils.Converters;
using NexusPoint.Utils;
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
    public class ReturnManager : INotifyPropertyChanged
    {
        private readonly CheckRepository _checkRepository;
        private readonly ProductRepository _productRepository;
        private readonly StockItemRepository _stockItemRepository;

        private Check _originalCheck;
        public Check OriginalCheck
        {
            get => _originalCheck;
            private set { _originalCheck = value; OnPropertyChanged(); } // Уведомляем об изменении
        }

        private ObservableCollection<ReturnItemView> _returnItems = new ObservableCollection<ReturnItemView>();
        // Публичное свойство для привязки
        public ReadOnlyObservableCollection<ReturnItemView> ReturnItems { get; }

        private string _calculatedReturnMethod = "-";
        public string CalculatedReturnMethod
        {
            get => _calculatedReturnMethod;
            private set { _calculatedReturnMethod = value; OnPropertyChanged(); } // Уведомляем
        }

        // Свойства для привязки итогов и состояния кнопки
        public decimal TotalReturnAmount => Math.Round(_returnItems.Sum(item => item.ReturnItemTotalAmount), 2);
        public bool CanProcessReturn => TotalReturnAmount > 0;

        // Событие INotifyPropertyChanged
        public event PropertyChangedEventHandler PropertyChanged;

        public ReturnManager(CheckRepository checkRepository, ProductRepository productRepository, StockItemRepository stockItemRepository)
        {
            _checkRepository = checkRepository ?? throw new ArgumentNullException(nameof(checkRepository));
            _productRepository = productRepository ?? throw new ArgumentNullException(nameof(productRepository));
            _stockItemRepository = stockItemRepository ?? throw new ArgumentNullException(nameof(stockItemRepository));
            ReturnItems = new ReadOnlyObservableCollection<ReturnItemView>(_returnItems);
        }

        public async Task<bool> FindOriginalCheckAsync(int checkNumber, int shiftNumber)
        {
            ClearCurrentReturn(); // Очищаем перед поиском
            try
            {
                var foundCheck = await Task.Run(() => _checkRepository.FindCheckByNumberAndShift(checkNumber, shiftNumber));

                if (foundCheck == null) return false;
                if (foundCheck.IsReturn)
                {
                    throw new InvalidOperationException($"Чек №{checkNumber} уже является чеком возврата.");
                }

                // Загружаем детали для отображения
                List<int> productIds = foundCheck.Items.Select(i => i.ProductId).Distinct().ToList();
                var products = (await Task.Run(() => _productRepository.GetProductsByIds(productIds)))
                               .ToDictionary(p => p.ProductId);

                // Отписываемся от старых перед добавлением новых
                foreach (var item in _returnItems) item.PropertyChanged -= ItemView_PropertyChanged;
                _returnItems.Clear(); // Очищаем коллекцию

                foreach (var item in foundCheck.Items)
                {
                    Product product = products.TryGetValue(item.ProductId, out Product p) ? p : null;
                    var itemView = new ReturnItemView(item, product);
                    itemView.PropertyChanged += ItemView_PropertyChanged;
                    _returnItems.Add(itemView);
                }

                OriginalCheck = foundCheck; // Устанавливаем найденный чек ПОСЛЕ заполнения _returnItems
                RecalculateTotalsAndMethod(); // Пересчитываем итоги и метод один раз
                return true;
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка поиска чека: {ex.Message}", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Warning);
                ClearCurrentReturn(); // Очищаем в случае ошибки
                return false;
            }
        }

        private void ItemView_PropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(ReturnItemView.ReturnQuantity))
            {
                RecalculateTotalsAndMethod(); // Пересчитываем при изменении количества
            }
        }

        // Объединенный метод пересчета
        private void RecalculateTotalsAndMethod()
        {
            CalculateReturnMethod(); // Сначала метод
            OnPropertyChanged(nameof(TotalReturnAmount)); // Затем уведомляем об изменении свойств
            OnPropertyChanged(nameof(CanProcessReturn));
            // CalculatedReturnMethod уже вызовет OnPropertyChanged сам
        }


        public void SetReturnQuantityForAll(bool returnFullQuantity)
        {
            if (OriginalCheck == null) return;
            foreach (var item in _returnItems) item.PropertyChanged -= ItemView_PropertyChanged;
            foreach (var item in _returnItems) { if (item.CanEditReturnQuantity) item.ReturnQuantity = returnFullQuantity ? item.Quantity : 0; }
            foreach (var item in _returnItems) item.PropertyChanged += ItemView_PropertyChanged;
            RecalculateTotalsAndMethod();
        }

        public void SetReturnQuantityForSelected(IEnumerable<ReturnItemView> selectedItems, bool returnFullQuantity)
        {
            if (OriginalCheck == null || selectedItems == null) return;
            foreach (var item in _returnItems) item.PropertyChanged -= ItemView_PropertyChanged;

            foreach (var item in _returnItems)
            {
                if (item.CanEditReturnQuantity)
                {
                    bool isSelected = selectedItems.Any(si => si.OriginalItem.CheckItemId == item.OriginalItem.CheckItemId);
                    if (isSelected) item.ReturnQuantity = returnFullQuantity ? item.Quantity : 0;
                    // else { item.ReturnQuantity = 0; } // Раскомментировать, если нужно сбрасывать невыбранные
                }
            }
            foreach (var item in _returnItems) item.PropertyChanged += ItemView_PropertyChanged;
            RecalculateTotalsAndMethod();
        }


        public void ClearCurrentReturn()
        {
            foreach (var item in _returnItems) item.PropertyChanged -= ItemView_PropertyChanged;
            _returnItems.Clear();
            OriginalCheck = null; // Вызовет OnPropertyChanged
            RecalculateTotalsAndMethod(); // Обновит остальные связанные свойства
        }

        private void CalculateReturnMethod()
        {
            string newMethod = "-";
            if (OriginalCheck != null)
            {
                decimal totalReturn = TotalReturnAmount;
                string originalPaymentTypeLower = OriginalCheck.PaymentType?.ToLower();

                if (totalReturn <= 0) { newMethod = "-"; }
                else if (originalPaymentTypeLower == "cash") { newMethod = "Наличными"; }
                else if (originalPaymentTypeLower == "card") { newMethod = "На карту"; }
                else if (originalPaymentTypeLower == "mixed")
                {
                    if (totalReturn <= OriginalCheck.CardPaid) { newMethod = "На карту"; }
                    else { newMethod = "Карта + Наличные"; }
                }
                else { newMethod = "Наличными (неизв. тип оплаты)"; }
            }
            // Устанавливаем значение и вызываем OnPropertyChanged только если оно изменилось
            if (_calculatedReturnMethod != newMethod)
            {
                CalculatedReturnMethod = newMethod; // Свойство само вызовет OnPropertyChanged
            }
        }

        public async Task<bool> ProcessReturnAsync(Shift currentShift, User currentUser)
        {
            if (!CanProcessReturn || OriginalCheck == null || currentShift == null || currentUser == null)
            {
                MessageBox.Show("Не выполнены условия для оформления возврата.", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Warning);
                return false;
            }

            var itemsToReturnView = _returnItems.Where(i => i.ReturnQuantity > 0).ToList();
            if (!itemsToReturnView.Any())
            {
                MessageBox.Show("Не выбраны позиции или количество для возврата.", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Warning);
                return false;
            }

            List<CheckItem> returnCheckItems = new List<CheckItem>();
            foreach (var viewItem in itemsToReturnView)
            {
                decimal quantityRatio = viewItem.Quantity > 0 ? viewItem.ReturnQuantity / viewItem.Quantity : 0;
                var newItem = new CheckItem
                {
                    ProductId = viewItem.ProductId,
                    Quantity = viewItem.ReturnQuantity,
                    PriceAtSale = viewItem.PriceAtSale,
                    DiscountAmount = Math.Round(viewItem.OriginalItem.DiscountAmount * quantityRatio, 2),
                    AppliedDiscountId = viewItem.OriginalItem.AppliedDiscountId
                };
                newItem.ItemTotalAmount = Math.Round(newItem.Quantity * newItem.PriceAtSale - newItem.DiscountAmount, 2);
                returnCheckItems.Add(newItem);
            }

            decimal returnTotalAmount = returnCheckItems.Sum(i => i.ItemTotalAmount);
            decimal returnDiscountAmount = returnCheckItems.Sum(i => i.DiscountAmount);

            bool returnByCard = false;
            bool returnByCash = false;
            decimal amountToReturnOnCard = 0m;
            decimal amountToReturnInCash = 0m;
            string returnPaymentTypeForCheck = "Cash";

            string originalPaymentTypeLower = OriginalCheck.PaymentType?.ToLower();

            if (originalPaymentTypeLower == "cash") { returnByCash = true; amountToReturnInCash = returnTotalAmount; returnPaymentTypeForCheck = "Cash"; }
            else if (originalPaymentTypeLower == "card") { returnByCard = true; amountToReturnOnCard = returnTotalAmount; returnPaymentTypeForCheck = "Card"; }
            else if (originalPaymentTypeLower == "mixed")
            {
                decimal originalCardPaid = OriginalCheck.CardPaid;
                if (returnTotalAmount <= originalCardPaid) { returnByCard = true; amountToReturnOnCard = returnTotalAmount; returnPaymentTypeForCheck = "Card"; }
                else { returnByCard = true; returnByCash = true; amountToReturnOnCard = originalCardPaid; amountToReturnInCash = returnTotalAmount - originalCardPaid; returnPaymentTypeForCheck = "Mixed"; }
            }
            else { returnByCash = true; amountToReturnInCash = returnTotalAmount; returnPaymentTypeForCheck = "Cash"; }

            if (amountToReturnInCash < 0 || amountToReturnOnCard < 0) { MessageBox.Show("Ошибка расчета суммы возврата.", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error); return false; }
            if (!returnByCard && !returnByCash) { MessageBox.Show("Не удалось определить метод возврата.", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error); return false; }

            var returnCheck = new Check
            {
                ShiftId = currentShift.ShiftId,
                CheckNumber = _checkRepository.GetNextCheckNumber(currentShift.ShiftId),
                Timestamp = DateTime.Now,
                UserId = currentUser.UserId,
                TotalAmount = returnTotalAmount,
                PaymentType = returnPaymentTypeForCheck,
                CashPaid = 0,
                CardPaid = 0,
                DiscountAmount = returnDiscountAmount,
                IsReturn = true,
                OriginalCheckId = OriginalCheck.CheckId,
                Items = returnCheckItems
            };

            try
            {
                if (returnByCard && amountToReturnOnCard > 0)
                {
                    MessageBoxResult pinpadResult = MessageBox.Show(
                        $"Банковский терминал:\nВОЗВРАТ на карту\nСумма: {amountToReturnOnCard:C}\n\nОперация прошла успешно?",
                        "Возврат на карту", MessageBoxButton.YesNo, MessageBoxImage.Question);
                    if (pinpadResult == MessageBoxResult.No) { MessageBox.Show("Возврат на карту отменен.", "Отмена", MessageBoxButton.OK, MessageBoxImage.Information); return false; }
                }

                var savedReturnCheck = await Task.Run(() => _checkRepository.AddCheck(returnCheck));
                await PrintReturnCheckReceiptAsync(savedReturnCheck, currentUser);

                if (returnByCash && amountToReturnInCash > 0)
                {
                    PrintCashExpenseOrder(savedReturnCheck, amountToReturnInCash, currentUser);
                    PrinterService.OpenCashDrawer();
                }

                // Очищаем состояние только после ПОЛНОГО успеха
                ClearCurrentReturn();
                return true;
            }
            catch (InvalidOperationException invEx) { MessageBox.Show($"Не удалось оформить возврат: {invEx.Message}", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error); return false; }
            catch (Exception ex) { MessageBox.Show($"Критическая ошибка при оформлении возврата: {ex.Message}", "Критическая ошибка", MessageBoxButton.OK, MessageBoxImage.Error); return false; }
        }

        private async Task PrintReturnCheckReceiptAsync(Check returnCheck, User cashier)
        {
            StringBuilder checkSb = new StringBuilder();
            checkSb.AppendLine($"--- Чек Возврата №{returnCheck.CheckNumber} ---");
            if (OriginalCheck != null) // Добавим проверку
                checkSb.AppendLine($"Основание: Чек продажи №{OriginalCheck.CheckNumber} от {OriginalCheck.Timestamp:d}");
            checkSb.AppendLine($"Кассир: {cashier.FullName}");
            checkSb.AppendLine($"Дата: {returnCheck.Timestamp:G}");
            checkSb.AppendLine("---------------------------------");

            var productsInfo = (await Task.Run(() => _productRepository.GetProductsByIds(returnCheck.Items.Select(i => i.ProductId))))
                                     .ToDictionary(p => p.ProductId);

            foreach (var item in returnCheck.Items)
            {
                string name = productsInfo.TryGetValue(item.ProductId, out var p) ? p.Name : "<Товар?>";
                checkSb.AppendLine($"{name} x {item.Quantity}");
                decimal pricePerUnit = item.PriceAtSale;
                decimal discountPerUnit = item.Quantity > 0 ? item.DiscountAmount / item.Quantity : 0;
                checkSb.AppendLine($"  Цена: {pricePerUnit:C} {(discountPerUnit > 0 ? $"(Скидка: {discountPerUnit:C})" : "")}");
                checkSb.AppendLine($"  Сумма: {item.ItemTotalAmount:C}");
            }
            checkSb.AppendLine("---------------------------------");
            checkSb.AppendLine($"ИТОГО К ВОЗВРАТУ: {returnCheck.TotalAmount:C}");

            string returnMethod = CalculatedReturnMethod; // Используем свойство
            if (returnMethod == "Карта + Наличные") checkSb.AppendLine("ВОЗВРАТ: КАРТА + НАЛИЧНЫЕ");
            else if (returnMethod == "На карту") checkSb.AppendLine("ВОЗВРАТ НА КАРТУ");
            else checkSb.AppendLine("ВОЗВРАТ НАЛИЧНЫМИ");

            checkSb.AppendLine("--- Конец чека возврата ---");
            PrinterService.Print($"Чек возврата №{returnCheck.CheckNumber}", checkSb.ToString());
        }

        private void PrintCashExpenseOrder(Check returnCheck, decimal amountToReturnInCash, User cashier)
        {
            StringBuilder rkoSb = new StringBuilder();
            rkoSb.AppendLine($"          Расходный Кассовый Ордер № {returnCheck.CheckNumber}-В");
            rkoSb.AppendLine($"                          от {returnCheck.Timestamp:d} г.");
            rkoSb.AppendLine("-------------------------------------------------------------");
            rkoSb.AppendLine($"Организация:    ООО \"NexusPoint\"");
            rkoSb.AppendLine("-------------------------------------------------------------");
            rkoSb.AppendLine($"Выдать:         _____________________________________________");
            rkoSb.AppendLine($"                              (Ф.И.О.)");
            if (OriginalCheck != null) // Добавим проверку
                rkoSb.AppendLine($"Основание:      Возврат товара по чеку №{OriginalCheck.CheckNumber} от {OriginalCheck.Timestamp:d}");
            else
                rkoSb.AppendLine($"Основание:      Возврат товара по чеку возврата №{returnCheck.CheckNumber}");
            rkoSb.AppendLine($"Сумма:          {amountToReturnInCash:N2} руб.");
            try { rkoSb.AppendLine($"                ({AmountToWordsConverter.Convert(amountToReturnInCash)})"); }
            catch { rkoSb.AppendLine("                (сумма прописью - ошибка)"); }
            rkoSb.AppendLine($"Приложение:     Чек №{returnCheck.CheckNumber}");
            rkoSb.AppendLine("\n");
            rkoSb.AppendLine("Получил:        ____________________  _____________________");
            rkoSb.AppendLine("                      (сумма прописью)");
            rkoSb.AppendLine($"                {returnCheck.Timestamp:d} г.             _____________________");
            rkoSb.AppendLine($"                                        (подпись)");
            rkoSb.AppendLine("\nПредъявлен документ: Паспорт серия _______ № _______________");
            rkoSb.AppendLine($"Выдан:          _____________________________________________");
            rkoSb.AppendLine($"                          (кем и когда)");
            rkoSb.AppendLine("\n");
            rkoSb.AppendLine("Главный бухгалтер _________ /_______________/");
            rkoSb.AppendLine($"Кассир            _________ / {cashier.FullName} /");

            PrinterService.Print($"РКО №{returnCheck.CheckNumber}-В", rkoSb.ToString());
        }

        protected virtual void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}