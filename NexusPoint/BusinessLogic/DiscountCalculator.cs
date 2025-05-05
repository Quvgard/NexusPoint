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
    public static class DiscountCalculator
    {
        // В реальном приложении репозиторий лучше передавать через DI или параметры
        private static readonly DiscountRepository _discountRepository = new DiscountRepository();
        private static readonly ProductRepository _productRepository = new ProductRepository(); // Может понадобиться для акций с подарком

        // --- Основной метод, вызываемый извне ---
        /// <summary>
        /// Применяет все применимые автоматические скидки к чеку.
        /// Сначала применяет скидки на позиции, затем скидки на весь чек.
        /// Модифицирует DiscountAmount и AppliedDiscountId позиций.
        /// </summary>
        /// <param name="checkItems">Список позиций чека (базовые CheckItem).</param>
        /// <returns>Общая сумма ВСЕХ примененных скидок.</returns>
        public static decimal ApplyAllAutoDiscounts(IList<CheckItem> checkItems) // Работаем с базовым CheckItem
        {
            if (checkItems == null || !checkItems.Any())
            {
                return 0m;
            }

            decimal totalDiscountApplied = 0m;
            List<Discount> activeDiscounts;

            try
            {
                activeDiscounts = _discountRepository.GetAllActiveDiscounts().ToList();
                if (!activeDiscounts.Any()) return 0m; // Нет активных скидок - выходим
            }
            catch (Exception ex)
            {
                // Ошибка загрузки скидок - не применяем их
                System.Diagnostics.Debug.WriteLine($"Error loading active discounts: {ex.Message}");
                MessageBox.Show($"Не удалось загрузить активные скидки: {ex.Message}", "Ошибка скидок", MessageBoxButton.OK, MessageBoxImage.Warning);
                return 0m;
            }

            // Сброс предыдущих скидок (если этот метод вызывается повторно)
            foreach (var item in checkItems)
            {
                item.DiscountAmount = 0m;
                item.AppliedDiscountId = null;
            }

            // Логика применения скидок (упрощенная):
            // 1. Применяем скидки на конкретные товары (RequiredProductId)
            // 2. Можно добавить логику для общих скидок (без RequiredProductId)
            // 3. Можно добавить обработку акций с подарками (Type='Gift')
            // 4. Важно: решить, может ли на одну позицию применяться несколько скидок.
            //    Пока реализуем: ОДНА самая выгодная скидка на позицию.

            // 1. Сброс предыдущих скидок
            foreach (var item in checkItems)
            {
                item.DiscountAmount = 0m;
                item.AppliedDiscountId = null;
                // Пересчитываем ItemTotalAmount НАЧАЛЬНЫЙ (без скидок)
                item.ItemTotalAmount = Math.Round(item.Quantity * item.PriceAtSale, 2);
            }

            // 2. Применение скидок УРОВНЯ ПОЗИЦИИ
            ApplyLineItemDiscounts(checkItems, activeDiscounts);

            // 3. Применение скидок УРОВНЯ ЧЕКА
            ApplyCheckLevelDiscounts(checkItems, activeDiscounts);

            // 4. Финальный пересчет ItemTotalAmount и расчет общей скидки
            decimal totalDiscount = 0m;
            foreach (var item in checkItems)
            {
                // Убедимся, что скидка не больше суммы позиции
                item.DiscountAmount = Math.Max(0, Math.Min(item.Quantity * item.PriceAtSale, item.DiscountAmount));
                item.ItemTotalAmount = Math.Round(item.Quantity * item.PriceAtSale - item.DiscountAmount, 2);
                totalDiscount += item.DiscountAmount;
            }

            // Логика для N+M, N-ный, Подарок - пока не здесь

            return Math.Round(totalDiscount, 2);
        }


        // --- Применение скидок на уровне позиции ---
        private static void ApplyLineItemDiscounts(IList<CheckItem> checkItems, List<Discount> activeDiscounts)
        {
            // Фильтруем скидки, которые могут применяться к позициям
            var lineItemDiscountTypes = new[] { "Процент", "Сумма", "Фикс. цена" };
            var lineDiscounts = activeDiscounts.Where(d => lineItemDiscountTypes.Contains(d.Type)).ToList();
            if (!lineDiscounts.Any()) return;


            foreach (var item in checkItems)
            {
                Discount bestDiscountInfo = null;
                decimal bestDiscountAmount = 0m;

                // Ищем лучшую скидку для этой позиции
                var applicableDiscounts = lineDiscounts
                    .Where(d => d.RequiredProductId == item.ProductId || d.RequiredProductId == null) // На товар или общая
                    .ToList(); // Материализуем для расчета

                foreach (var discount in applicableDiscounts)
                {
                    decimal potentialDiscount = CalculateLineItemDiscountAmount(item, discount);
                    // Применяем самую выгодную (наибольшую сумму скидки)
                    if (potentialDiscount > bestDiscountAmount)
                    {
                        bestDiscountAmount = potentialDiscount;
                        bestDiscountInfo = discount;
                    }
                }

                // Применяем лучшую найденную скидку
                if (bestDiscountInfo != null && bestDiscountAmount > 0)
                {
                    // ВАЖНО: Пока просто ПРИБАВЛЯЕМ скидку. Логика совмещения скидок может быть сложнее.
                    // Сейчас, если сработает скидка на чек, она добавится к этой.
                    item.DiscountAmount += Math.Round(bestDiscountAmount, 2);
                    // Записываем ID последней примененной скидки на позицию
                    // Если нужно хранить ВСЕ - нужна другая структура
                    item.AppliedDiscountId = bestDiscountInfo.DiscountId;
                }
            }
        }

        // Расчет суммы скидки для ТИПОВ, действующих на позицию
        private static decimal CalculateLineItemDiscountAmount(CheckItem item, Discount discount)
        {
            decimal itemSubTotal = item.Quantity * item.PriceAtSale;
            decimal discountAmount = 0m;

            switch (discount.Type)
            {
                case "Процент":
                    if (discount.Value.HasValue && discount.Value.Value > 0)
                        discountAmount = itemSubTotal * (discount.Value.Value / 100m);
                    break;
                case "Сумма":
                    if (discount.Value.HasValue && discount.Value.Value > 0)
                        discountAmount = Math.Min(itemSubTotal, discount.Value.Value); // Скидка на всю позицию
                    break;
                case "Фикс. цена":
                    if (discount.Value.HasValue && discount.Value.Value >= 0)
                    {
                        decimal fixedPriceTotal = item.Quantity * discount.Value.Value;
                        if (fixedPriceTotal < itemSubTotal)
                            discountAmount = itemSubTotal - fixedPriceTotal;
                    }
                    break;
            }
            return Math.Max(0, discountAmount); // Не меньше нуля
        }


        // --- Применение скидок на уровне чека ---
        private static void ApplyCheckLevelDiscounts(IList<CheckItem> checkItems, List<Discount> activeDiscounts)
        {
            var checkDiscountRules = activeDiscounts.Where(d => d.Type == "Скидка на сумму чека").ToList();
            if (!checkDiscountRules.Any()) return;

            // Считаем сумму чека ДО скидок на чек, но ПОСЛЕ скидок на позиции
            decimal currentCheckTotal = checkItems.Sum(i => i.Quantity * i.PriceAtSale - i.DiscountAmount);
            if (currentCheckTotal <= 0) return; // Нет смысла применять к нулевой сумме

            Discount bestCheckDiscountInfo = null;
            decimal bestCheckDiscountAmount = 0m;

            // Ищем лучшую скидку НА ЧЕК
            foreach (var discount in checkDiscountRules)
            {
                // Проверяем порог суммы
                if (discount.CheckAmountThreshold.HasValue && currentCheckTotal >= discount.CheckAmountThreshold.Value)
                {
                    decimal potentialDiscount = 0m;
                    if (discount.Value.HasValue && discount.Value.Value > 0)
                    {
                        if (discount.IsCheckDiscountPercentage) // Процент от ТЕКУЩЕЙ суммы чека
                        {
                            potentialDiscount = currentCheckTotal * (discount.Value.Value / 100m);
                        }
                        else // Фиксированная сумма
                        {
                            potentialDiscount = Math.Min(currentCheckTotal, discount.Value.Value);
                        }
                    }

                    // Выбираем самую выгодную скидку на чек
                    if (potentialDiscount > bestCheckDiscountAmount)
                    {
                        bestCheckDiscountAmount = potentialDiscount;
                        bestCheckDiscountInfo = discount;
                    }
                }
            }
            // Если нашли подходящую скидку на чек, распределяем ее по позициям
            if (bestCheckDiscountInfo != null && bestCheckDiscountAmount > 0)
            {
                decimal totalCheckAmountBeforeCheckDiscount = checkItems.Sum(i => i.Quantity * i.PriceAtSale - i.DiscountAmount); // Сумма до скидки на чек
                if (totalCheckAmountBeforeCheckDiscount <= 0) return; // Защита

                decimal totalCheckDiscountToApply = Math.Round(bestCheckDiscountAmount, 2);
                decimal appliedSum = 0m;

                // Распределяем пропорционально СУММЕ ПОЗИЦИИ (уже с учетом скидок на позицию)
                for (int i = 0; i < checkItems.Count; i++)
                {
                    var item = checkItems[i];
                    // Пересчитываем сумму позиции перед расчетом доли
                    decimal currentItemTotal = item.Quantity * item.PriceAtSale - item.DiscountAmount;
                    if (currentItemTotal <= 0) continue; // Не распределяем на нулевые/отрицательные позиции

                    decimal itemShare = (currentItemTotal / totalCheckAmountBeforeCheckDiscount); // Доля в сумме до скидки на чек
                    decimal discountPortion = 0m;

                    if (i == checkItems.Count - 1) // Последняя позиция забирает остаток
                    {
                        discountPortion = totalCheckDiscountToApply - appliedSum;
                    }
                    else
                    {
                        discountPortion = Math.Round(totalCheckDiscountToApply * itemShare, 2);
                    }

                    // Добавляем скидку к существующей, но не больше остатка суммы
                    decimal maxPossibleDiscount = currentItemTotal; // Максимум - вся текущая сумма позиции
                    decimal discountToAdd = Math.Max(0, Math.Min(maxPossibleDiscount, discountPortion));

                    item.DiscountAmount += discountToAdd;
                    // ID скидки перезаписываем на скидку чека? Или хранить несколько?
                    // Пока перезаписываем на ID скидки чека, если она применилась.
                    if (discountToAdd > 0)
                    {
                        item.AppliedDiscountId = bestCheckDiscountInfo.DiscountId;
                    }
                    appliedSum += discountToAdd;
                }

                // TODO: Обработка разницы из-за округления (как в ApplyManualCheckDiscount)
            }
        }


        /// <summary>
        /// Рассчитывает ПОТЕНЦИАЛЬНУЮ сумму скидки для одной позиции по правилу скидки.
        /// </summary>
        private static decimal CalculatePotentialDiscount(CheckItem item, Discount discount)
        {
            decimal itemSubTotal = item.Quantity * item.PriceAtSale; // Сумма позиции по цене продажи
            decimal discountAmount = 0m;

            switch (discount.Type) // Используем русские названия
            {
                case "Процент": // Percentage
                    if (discount.Value.HasValue && discount.Value.Value > 0)
                    {
                        discountAmount = itemSubTotal * (discount.Value.Value / 100m);
                    }
                    break;
                case "Сумма": // FixedAmount
                    if (discount.Value.HasValue && discount.Value.Value > 0)
                    {
                        discountAmount = Math.Min(itemSubTotal, discount.Value.Value);
                    }
                    break;
                case "Фикс. цена": // FixedPrice - НОВЫЙ ТИП
                    if (discount.Value.HasValue && discount.Value.Value >= 0) // Цена может быть 0
                    {
                        // Скидка = (Обычная цена * Кол-во) - (Фикс. цена * Кол-во)
                        // Только если фикс. цена МЕНЬШЕ обычной цены продажи
                        decimal fixedPriceTotal = item.Quantity * discount.Value.Value;
                        if (fixedPriceTotal < itemSubTotal)
                        {
                            discountAmount = itemSubTotal - fixedPriceTotal;
                        }
                        // Если фикс. цена выше или равна, скидки нет (discountAmount = 0)
                    }
                    break;
                case "Подарок": // Gift
                                // Подарок не дает денежной скидки на ЭТУ позицию
                    discountAmount = 0m;
                    break;
            }

            // Убедимся, что скидка не отрицательная и не больше суммы
            return Math.Max(0, Math.Min(itemSubTotal, discountAmount));
        }

        /// <summary>
        /// Применяет ручную скидку (процент или сумма) ко всему чеку,
        /// пропорционально распределяя ее по позициям.
        /// Модифицирует DiscountAmount элементов списка.
        /// </summary>
        /// <param name="checkItems">Позиции чека.</param>
        /// <param name="discountValue">Значение скидки (процент или сумма).</param>
        /// <param name="isPercentage">True - скидка в процентах, False - в сумме.</param>
        /// <returns>Общая сумма примененной ручной скидки.</returns>
        public static decimal ApplyManualCheckDiscount(IList<CheckItem> checkItems, decimal discountValue, bool isPercentage)
        {
            if (checkItems == null || !checkItems.Any() || discountValue <= 0)
            {
                return 0m;
            }

            decimal totalCheckAmount = checkItems.Sum(i => i.Quantity * i.PriceAtSale); // Сумма чека ДО скидок
            if (totalCheckAmount <= 0) return 0m; // Нельзя применить скидку к нулевой сумме

            decimal totalDiscountToApply = 0m;

            if (isPercentage)
            {
                if (discountValue > 100) discountValue = 100; // Ограничение 100%
                totalDiscountToApply = totalCheckAmount * (discountValue / 100m);
            }
            else // Фиксированная сумма
            {
                totalDiscountToApply = Math.Min(totalCheckAmount, discountValue); // Скидка не больше суммы чека
            }

            totalDiscountToApply = Math.Round(totalDiscountToApply, 2); // Округляем общую скидку

            // Распределяем скидку пропорционально стоимости позиций
            decimal appliedSum = 0m;
            for (int i = 0; i < checkItems.Count; i++)
            {
                var item = checkItems[i];
                decimal itemSubTotal = item.Quantity * item.PriceAtSale;

                // Если это последняя позиция, отдаем ей всю оставшуюся скидку (для точности)
                if (i == checkItems.Count - 1)
                {
                    item.DiscountAmount = Math.Max(0, Math.Min(itemSubTotal, totalDiscountToApply - appliedSum));
                }
                else
                {
                    decimal itemShare = (itemSubTotal / totalCheckAmount); // Доля этой позиции в общей сумме
                    decimal discountForItem = Math.Round(totalDiscountToApply * itemShare, 2);
                    // Убедимся, что скидка на позицию не больше ее суммы
                    item.DiscountAmount = Math.Max(0, Math.Min(itemSubTotal, discountForItem));
                }
                appliedSum += item.DiscountAmount;
            }

            // Корректировка из-за округления (если общая примененная скидка отличается от целевой)
            decimal difference = totalDiscountToApply - appliedSum;
            if (Math.Abs(difference) > 0.001m && checkItems.Any())
            {
                // Добавляем/убираем разницу с первой позиции (которая может ее вместить)
                var firstItem = checkItems.First();
                if (firstItem.Quantity * firstItem.PriceAtSale >= firstItem.DiscountAmount + difference)
                {
                    firstItem.DiscountAmount += difference;
                    appliedSum += difference;
                }
            }


            return Math.Round(appliedSum, 2);
        }
    }
}