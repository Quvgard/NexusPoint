using NexusPoint.Data.Repositories;
using NexusPoint.Models;
using NexusPoint.Utils;
using NexusPoint.Windows;
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
        // DiscountCalculator используется статически

        public PaymentProcessor(CheckRepository checkRepository, ProductRepository productRepository, StockItemRepository stockItemRepository)
        {
            _checkRepository = checkRepository ?? throw new ArgumentNullException(nameof(checkRepository));
            _productRepository = productRepository ?? throw new ArgumentNullException(nameof(productRepository));
            _stockItemRepository = stockItemRepository ?? throw new ArgumentNullException(nameof(stockItemRepository));
        }

        /// <summary>
        /// Обрабатывает оплату чека: применяет автоматические скидки, сохраняет чек, печатает его.
        /// </summary>
        /// <param name="currentItemsView">Текущие позиции чека из SaleManager.</param>
        /// <param name="isManualDiscountApplied">Флаг, была ли применена ручная скидка.</param>
        /// <param name="currentShift">Текущая открытая смена.</param>
        /// <param name="currentUser">Текущий пользователь.</param>
        /// <param name="paymentType">Выбранный тип оплаты ("Cash", "Card", "Mixed").</param>
        /// <param name="cashPaid">Сумма, полученная наличными.</param>
        /// <param name="cardPaid">Сумма, оплаченная картой (рассчитанная в PaymentDialog).</param>
        /// <param name="change">Сдача (рассчитанная в PaymentDialog).</param>
        /// <returns>Объект сохраненного чека или null в случае ошибки/отмены.</returns>
        public async Task<Check> ProcessPaymentAsync(
            IEnumerable<CheckItemView> currentItemsView, // Теперь это УЖЕ элементы с примененными скидками
            bool isManualDiscountApplied, // Этот флаг больше не используется для расчета, но может быть полезен для логов
            Shift currentShift,
            User currentUser,
            string paymentType,
            decimal cashPaid,
            decimal cardPaid,
            decimal change)
        {
            // ... (проверки currentShift, currentUser, currentItemsView) ...

            // --- УДАЛЕНО: Расчет автоматических скидок здесь больше не нужен ---
            // DiscountCalculationResult discountResult = null;
            // List<CheckItem> itemsToSave;
            // if (!isManualDiscountApplied) { ... вызов DiscountCalculator ... }
            // else { ... }
            // --- КОНЕЦ УДАЛЕНИЯ ---

            // 1. Используем переданные элементы как основу для сохранения
            List<CheckItem> itemsToSave = currentItemsView.Select(civ => new CheckItem
            {
                // Копируем данные из CheckItemView в CheckItem
                ProductId = civ.ProductId,
                Quantity = civ.Quantity,
                PriceAtSale = civ.PriceAtSale,
                DiscountAmount = civ.DiscountAmount,
                AppliedDiscountId = civ.AppliedDiscountId,
                ItemTotalAmount = civ.CalculatedItemTotalAmount // Берем рассчитанную сумму
            }).ToList();


            // 2. Пересчитываем финальные итоги (на всякий случай, хотя они должны совпадать с SaleManager)
            decimal finalTotalDiscount = Math.Round(itemsToSave.Sum(i => i.DiscountAmount), 2);
            decimal finalTotalAmount = Math.Round(itemsToSave.Sum(i => i.ItemTotalAmount), 2);

            // 3. Проверка нулевой суммы (если это не только подарки)
            bool hasNonGiftItems = itemsToSave.Any(i => i.PriceAtSale > 0);
            if (finalTotalAmount <= 0 && hasNonGiftItems)
            {
                MessageBoxResult freeResult = MessageBox.Show($"Итоговая сумма чека {(finalTotalAmount < 0 ? "отрицательная" : "равна нулю")} из-за скидок. Завершить продажу бесплатно?",
                                   "Нулевая сумма", MessageBoxButton.YesNo, MessageBoxImage.Question);
                if (freeResult == MessageBoxResult.No)
                {
                    return null; // Отмена
                }
                // Если Да - сохраняем чек как оплаченный Наличными 0
                paymentType = "Cash";
                cashPaid = 0;
                cardPaid = 0;
                change = 0;
            }
            else if (!hasNonGiftItems && itemsToSave.Any()) // Только подарки
            {
                // Если в чеке только подарки (сумма 0), нужно ли их сохранять? Зависит от требований.
                // Пока будем сохранять.
                paymentType = "Cash"; // Условно
                cashPaid = 0;
                cardPaid = 0;
                change = 0;
            }
            else if (!itemsToSave.Any()) // Если список пуст (маловероятно)
            {
                MessageBox.Show("В чеке нет позиций для сохранения.", "Пустой чек", MessageBoxButton.OK, MessageBoxImage.Information);
                return null;
            }


            // 4. Формируем объект чека для сохранения
            var checkToSave = new Check
            {
                ShiftId = currentShift.ShiftId,
                CheckNumber = _checkRepository.GetNextCheckNumber(currentShift.ShiftId),
                Timestamp = DateTime.Now,
                UserId = currentUser.UserId,
                TotalAmount = finalTotalAmount, // <--- Используем пересчитанную сумму
                PaymentType = paymentType,
                CashPaid = cashPaid,
                CardPaid = cardPaid,
                DiscountAmount = finalTotalDiscount, // <--- Используем пересчитанную скидку
                IsReturn = false,
                OriginalCheckId = null,
                Items = itemsToSave
            };

            // 5. Сохранение чека и печать
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

        // Метод для печати чека продажи
        private async Task PrintSaleReceiptAsync(Check check, User cashier, decimal change)
        {
            StringBuilder sb = new StringBuilder();
            sb.AppendLine($"--- Чек №{check.CheckNumber} ---");
            sb.AppendLine($"ООО \"NexusPoint\""); // Пример
            sb.AppendLine($"Кассир: {cashier.FullName}");
            sb.AppendLine($"ИНН: 1234567890   ЗН ККТ: 00012345"); // Пример
            sb.AppendLine($"Смена №: {check.ShiftId}   Чек №: {check.CheckNumber}"); // TODO: Подгрузить номер смены?
            sb.AppendLine($"{check.Timestamp:G}");
            sb.AppendLine(check.IsReturn ? "*** ВОЗВРАТ ПРИХОДА ***" : "*** ПРИХОД ***");
            sb.AppendLine("---------------------------------");

            // Загрузка названий товаров
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
            sb.AppendLine($"ПОДЫТОГ: {(check.TotalAmount + check.DiscountAmount):N2}"); // Подытог = Итого + Скидка
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
            if (change > 0.001m) // Печатаем сдачу, если она есть
            {
                sb.AppendLine($"СДАЧА: {change:C}");
            }
            sb.AppendLine("---------------------------------");
            sb.AppendLine($"ФН: 999900001111222   ФД: {check.CheckId + 10000}  ФП: 1234567890"); // Пример
            sb.AppendLine("--- Спасибо за покупку! ---");

            PrinterService.Print($"Чек №{check.CheckNumber}", sb.ToString());
        }
    }
}