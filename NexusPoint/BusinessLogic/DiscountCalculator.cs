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

        /// <summary>
        /// Применяет автоматические скидки к списку позиций чека.
        /// Модифицирует свойство DiscountAmount у элементов списка.
        /// </summary>
        /// <param name="checkItems">Список позиций чека (CheckItem или CheckItemView).</param>
        /// <returns>Общая сумма примененных автоматических скидок.</returns>
        public static decimal ApplyAutoDiscounts(IList<CheckItem> checkItems) // Работаем с базовым CheckItem
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
            }

            // Логика применения скидок (упрощенная):
            // 1. Применяем скидки на конкретные товары (RequiredProductId)
            // 2. Можно добавить логику для общих скидок (без RequiredProductId)
            // 3. Можно добавить обработку акций с подарками (Type='Gift')
            // 4. Важно: решить, может ли на одну позицию применяться несколько скидок.
            //    Пока реализуем: ОДНА самая выгодная скидка на позицию.

            foreach (var item in checkItems)
            {
                decimal bestDiscountForItem = 0m;
                Discount appliedDiscountInfo = null; // Какая скидка была применена

                // Ищем скидки, применимые к ЭТОМУ товару
                var applicableDiscounts = activeDiscounts
                    .Where(d => d.RequiredProductId == item.ProductId || d.RequiredProductId == null) // Скидки на этот товар или общие
                    .OrderByDescending(d => CalculatePotentialDiscount(item, d)); // Сортируем по убыванию выгоды

                // Применяем первую (самую выгодную) найденную скидку
                var bestDiscount = applicableDiscounts.FirstOrDefault();

                if (bestDiscount != null)
                {
                    bestDiscountForItem = CalculatePotentialDiscount(item, bestDiscount);
                    appliedDiscountInfo = bestDiscount; // Запоминаем примененную скидку
                }


                // Применяем лучшую найденную скидку к позиции
                if (bestDiscountForItem > 0)
                {
                    // Округляем скидку до копеек
                    item.DiscountAmount = Math.Round(bestDiscountForItem, 2);
                    totalDiscountApplied += item.DiscountAmount;

                    // TODO: Сохранить информацию о примененной скидке (appliedDiscountInfo.DiscountId),
                    // если это нужно для печати расшифровки (потребует доработки модели CheckItem).
                    System.Diagnostics.Debug.WriteLine($"Applied discount '{appliedDiscountInfo?.Name}' ({item.DiscountAmount:C}) to item '{item.ProductId}'");
                }

                // TODO: Обработка акций с подарками (Type='Gift')
                // Если сработала акция с подарком, нужно будет добавить позицию с подарком в чек (возможно, с ценой 0)
                // Это лучше делать не здесь, а в SaleManager или CashierWindow после вызова ApplyAutoDiscounts.
            }


            return totalDiscountApplied;
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