using NexusPoint.Data.Repositories;
using NexusPoint.Models;
using NexusPoint.Utils;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;

namespace NexusPoint.BusinessLogic
{
    public class PaymentProcessor
    {
        private readonly CheckRepository _checkRepository;
        private readonly ProductRepository _productRepository;
        private readonly StockItemRepository _stockItemRepository;

        public PaymentProcessor(CheckRepository checkRepository, ProductRepository productRepository, StockItemRepository stockItemRepository)
        {
            _checkRepository = checkRepository ?? throw new ArgumentNullException(nameof(checkRepository));
            _productRepository = productRepository ?? throw new ArgumentNullException(nameof(productRepository));
            _stockItemRepository = stockItemRepository ?? throw new ArgumentNullException(nameof(stockItemRepository));
        }
        public async Task<Check> ProcessPaymentAsync(
            IEnumerable<CheckItemView> currentItemsView,
            bool isManualDiscountApplied,
            Shift currentShift,
            User currentUser,
            string paymentType,
            decimal cashPaid,
            decimal cardPaid,
            decimal change)
        {
            List<CheckItem> itemsToSave = currentItemsView.Select(civ => new CheckItem
            {
                ProductId = civ.ProductId,
                Quantity = civ.Quantity,
                PriceAtSale = civ.PriceAtSale,
                DiscountAmount = civ.DiscountAmount,
                AppliedDiscountId = civ.AppliedDiscountId,
                ItemTotalAmount = civ.CalculatedItemTotalAmount
            }).ToList();
            decimal finalTotalDiscount = Math.Round(itemsToSave.Sum(i => i.DiscountAmount), 2);
            decimal finalTotalAmount = Math.Round(itemsToSave.Sum(i => i.ItemTotalAmount), 2);
            bool hasNonGiftItems = itemsToSave.Any(i => i.PriceAtSale > 0);
            if (finalTotalAmount <= 0 && hasNonGiftItems)
            {
                MessageBoxResult freeResult = MessageBox.Show($"Итоговая сумма чека {(finalTotalAmount < 0 ? "отрицательная" : "равна нулю")} из-за скидок. Завершить продажу бесплатно?",
                                   "Нулевая сумма", MessageBoxButton.YesNo, MessageBoxImage.Question);
                if (freeResult == MessageBoxResult.No)
                {
                    return null;
                }
                paymentType = "Cash";
                cashPaid = 0;
                cardPaid = 0;
                change = 0;
            }
            else if (!hasNonGiftItems && itemsToSave.Any())
            {
                paymentType = "Cash";
                cashPaid = 0;
                cardPaid = 0;
                change = 0;
            }
            else if (!itemsToSave.Any())
            {
                MessageBox.Show("В чеке нет позиций для сохранения.", "Пустой чек", MessageBoxButton.OK, MessageBoxImage.Information);
                return null;
            }
            var checkToSave = new Check
            {
                ShiftId = currentShift.ShiftId,
                CheckNumber = _checkRepository.GetNextCheckNumber(currentShift.ShiftId),
                Timestamp = DateTime.Now,
                UserId = currentUser.UserId,
                TotalAmount = finalTotalAmount,
                PaymentType = paymentType,
                CashPaid = cashPaid,
                CardPaid = cardPaid,
                DiscountAmount = finalTotalDiscount,
                IsReturn = false,
                OriginalCheckId = null,
                Items = itemsToSave
            };
            try
            {
                var savedCheck = await Task.Run(() => _checkRepository.AddCheck(checkToSave));
                await PrintSaleReceiptAsync(savedCheck, currentUser, change);
                if (paymentType == "Cash" || (paymentType == "Mixed" && cashPaid > 0) || change > 0)
                {
                    PrinterService.OpenCashDrawer();
                }
                return savedCheck;
            }
            catch (InvalidOperationException invEx)
            {
                MessageBox.Show($"Не удалось сохранить чек (ошибка остатков или другая):\n{invEx.Message}", "Ошибка сохранения", MessageBoxButton.OK, MessageBoxImage.Error);
                return null;
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Критическая ошибка при сохранении чека:\n{ex.Message}", "Ошибка сохранения", MessageBoxButton.OK, MessageBoxImage.Error);
                return null;
            }
        }
        private async Task PrintSaleReceiptAsync(Check check, User cashier, decimal change)
        {
            StringBuilder sb = new StringBuilder();
            sb.AppendLine($"--- Чек №{check.CheckNumber} ---");
            sb.AppendLine($"ООО \"NexusPoint\"");
            sb.AppendLine($"Кассир: {cashier.FullName}");
            sb.AppendLine($"ИНН: 1234567890   ЗН ККТ: 00012345");
            sb.AppendLine($"Смена №: {check.ShiftId}   Чек №: {check.CheckNumber}");
            sb.AppendLine($"{check.Timestamp:G}");
            sb.AppendLine(check.IsReturn ? "*** ВОЗВРАТ ПРИХОДА ***" : "*** ПРИХОД ***");
            sb.AppendLine("---------------------------------");
            var productIds = check.Items.Select(i => i.ProductId).Distinct().ToList();
            var products = (await Task.Run(() => _productRepository.GetProductsByIds(productIds)))
                           .ToDictionary(p => p.ProductId);

            foreach (var item in check.Items)
            {
                string productName = products.TryGetValue(item.ProductId, out Product p) ? p.Name : "<Товар?>";
                sb.AppendLine($"{productName}");
                sb.AppendLine($"  {item.Quantity} x {item.PriceAtSale:N2} = {(item.Quantity * item.PriceAtSale):N2}");
                if (item.DiscountAmount > 0)
                {
                    sb.AppendLine($"  Скидка: {item.DiscountAmount:N2}");
                }
                sb.AppendLine($"  ИТОГ ПО ПОЗИЦИИ: {item.ItemTotalAmount:N2}");
            }
            sb.AppendLine("---------------------------------");
            sb.AppendLine($"ПОДЫТОГ: {(check.TotalAmount + check.DiscountAmount):N2}");
            if (check.DiscountAmount > 0)
            {
                sb.AppendLine($"СКИДКА НА ЧЕК: {check.DiscountAmount:N2}");
            }
            sb.AppendLine($"ИТОГО: {check.TotalAmount:N2}");
            sb.AppendLine("---------------------------------");
            string paymentTypeText = check.PaymentType == "Cash" ? "НАЛИЧНЫМИ" :
                                      check.PaymentType == "Card" ? "КАРТОЙ" : "СМЕШАННАЯ";
            sb.AppendLine($"ОПЛАТА ({paymentTypeText}): {check.TotalAmount:C}");
            if (check.PaymentType == "Cash" || check.PaymentType == "Mixed")
                sb.AppendLine($"  ПОЛУЧЕНО НАЛ: {check.CashPaid:C}");
            if (check.PaymentType == "Card" || check.PaymentType == "Mixed")
                sb.AppendLine($"  ПОЛУЧЕНО КАРТОЙ: {check.CardPaid:C}");
            if (change > 0.001m)
            {
                sb.AppendLine($"СДАЧА: {change:C}");
            }
            sb.AppendLine("---------------------------------");
            sb.AppendLine($"ФН: 999900001111222   ФД: {check.CheckId + 10000}  ФП: 1234567890");
            sb.AppendLine("--- Спасибо за покупку! ---");

            PrinterService.Print($"Чек №{check.CheckNumber}", sb.ToString());
        }
    }
}