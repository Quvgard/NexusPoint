using NexusPoint.Data.Repositories;
using NexusPoint.Models;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;

namespace NexusPoint.BusinessLogic
{
    public class DiscountCalculationResult
    {
        public decimal TotalDiscountAmount { get; set; } = 0m;
        public List<CheckItem> DiscountedItems { get; set; } = new List<CheckItem>();
        public List<CheckItem> GiftsToAdd { get; set; } = new List<CheckItem>();
        public Discount AppliedCheckDiscount { get; set; } = null;
        // Добавим флаг, была ли применена процентная скидка на чек
        public bool IsCheckDiscountPercentage { get; set; } = false;
        // Добавим значение скидки на чек для распределения
        public decimal? CheckDiscountValue { get; set; } = null;
    }

    public static class DiscountCalculator
    {
        private static readonly DiscountRepository _discountRepository = new DiscountRepository();
        private static readonly ProductRepository _productRepository = new ProductRepository();

        public static DiscountCalculationResult ApplyAllAutoDiscounts(IEnumerable<CheckItem> originalCheckItems)
        {
            var result = new DiscountCalculationResult();
            if (originalCheckItems == null || !originalCheckItems.Any()) return result;

            List<Discount> activeDiscounts;
            try
            {
                activeDiscounts = _discountRepository.GetAllActiveDiscounts().ToList();
                if (!activeDiscounts.Any())
                {
                    result.DiscountedItems = originalCheckItems.Select(CreateCleanCopy).ToList();
                    RecalculateTotals(result.DiscountedItems);
                    return result;
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error loading active discounts: {ex.Message}");
                MessageBox.Show($"Не удалось загрузить активные скидки: {ex.Message}. Скидки не будут применены.", "Ошибка скидок", MessageBoxButton.OK, MessageBoxImage.Warning);
                result.DiscountedItems = originalCheckItems.Select(CreateCleanCopy).ToList();
                RecalculateTotals(result.DiscountedItems);
                return result;
            }

            var workingItems = originalCheckItems.Select(CreateCleanCopy).ToList();

            ApplyNthItemDiscounts(workingItems, activeDiscounts);
            ApplyBestOfferPerUnit(workingItems, activeDiscounts, result.GiftsToAdd);
            GenerateNxMGifts(workingItems, activeDiscounts, result.GiftsToAdd);

            // Изменяем ApplyCheckLevelDiscounts, чтобы он возвращал информацию о скидке
            var checkDiscountInfo = FindBestCheckLevelDiscount(workingItems, activeDiscounts);
            if (checkDiscountInfo != null)
            {
                result.AppliedCheckDiscount = checkDiscountInfo.Discount;
                result.IsCheckDiscountPercentage = checkDiscountInfo.IsPercentage;
                result.CheckDiscountValue = checkDiscountInfo.Value; // Сохраняем значение для распределения

                ApplyCheckLevelDistribution(workingItems, result); // Выносим распределение
            }


            RecalculateTotals(workingItems);
            result.DiscountedItems = workingItems;
            result.TotalDiscountAmount = result.DiscountedItems.Sum(i => i.DiscountAmount);

            Debug.WriteLine($"Discount calculation finished. Total Discount: {result.TotalDiscountAmount:C}");
            return result;
        }

        private static void ApplyNthItemDiscounts(List<CheckItem> workingItems, List<Discount> activeDiscounts)
        {
            var nthDiscountRules = activeDiscounts.Where(d => d.Type == "Скидка на N-ный" && d.RequiredProductId.HasValue).ToList();
            if (!nthDiscountRules.Any()) return;

            var groupedByProduct = workingItems.GroupBy(i => i.ProductId);

            foreach (var group in groupedByProduct)
            {
                var itemsInGroup = group.ToList();
                var rulesForThisProduct = nthDiscountRules.Where(r => r.RequiredProductId == group.Key).ToList();
                if (!rulesForThisProduct.Any()) continue;

                var discountRule = rulesForThisProduct.First();

                if (!discountRule.NthItemNumber.HasValue || discountRule.NthItemNumber.Value <= 0 ||
                    !discountRule.Value.HasValue || discountRule.Value.Value <= 0) continue;

                int n = discountRule.NthItemNumber.Value;
                decimal totalQuantity = itemsInGroup.Sum(i => i.Quantity);
                int nthItemsCount = (int)Math.Floor(totalQuantity / n);

                if (nthItemsCount == 0) continue;

                decimal priceOfOneUnit = itemsInGroup.First().PriceAtSale;
                decimal discountPerNthUnit = 0m;

                if (discountRule.IsNthDiscountPercentage)
                {
                    discountPerNthUnit = priceOfOneUnit * (Math.Min(100, discountRule.Value.Value) / 100m);
                }
                else
                {
                    discountPerNthUnit = Math.Min(priceOfOneUnit, discountRule.Value.Value);
                }

                discountPerNthUnit = Math.Max(0, Math.Round(discountPerNthUnit, 2));
                if (discountPerNthUnit <= 0) continue;

                decimal quantityCounter = 0;
                int nthAppliedCount = 0;
                foreach (var item in itemsInGroup.OrderBy(i => workingItems.IndexOf(i)))
                {
                    decimal itemStartQty = quantityCounter;
                    decimal itemEndQty = quantityCounter + item.Quantity;
                    decimal itemDiscountApplied = 0m;

                    int firstNthIndexInItem = (int)Math.Ceiling((itemStartQty + 0.0001m) / n);
                    int lastNthIndexInItem = (int)Math.Floor(itemEndQty / n);
                    int nthCountInItem = Math.Max(0, lastNthIndexInItem - firstNthIndexInItem + 1);
                    nthCountInItem = Math.Min(nthCountInItem, nthItemsCount - nthAppliedCount);

                    if (nthCountInItem > 0)
                    {
                        itemDiscountApplied = discountPerNthUnit * nthCountInItem;
                        item.DiscountAmount += itemDiscountApplied;
                        item.AppliedDiscountId = discountRule.DiscountId;
                        nthAppliedCount += nthCountInItem;
                        Debug.WriteLine($"Applied Nth discount ({discountPerNthUnit:C} x {nthCountInItem}) to item {item.ProductId}. Total applied: {nthAppliedCount}/{nthItemsCount}");
                    }

                    quantityCounter = itemEndQty;
                    if (nthAppliedCount >= nthItemsCount) break;
                }
            }
        }

        private static void ApplyBestOfferPerUnit(List<CheckItem> workingItems, List<Discount> activeDiscounts, List<CheckItem> giftsToAdd)
        {
            var itemDiscountTypes = new[] { "Процент", "Сумма", "Фикс. цена" };
            var itemDiscounts = activeDiscounts.Where(d => itemDiscountTypes.Contains(d.Type)).ToList();
            var giftActions = activeDiscounts.Where(d => d.Type == "Подарок").ToList();
            var giftProductCache = new Dictionary<int, Product>();

            foreach (var item in workingItems)
            {
                decimal existingDiscountPerUnit = item.Quantity > 0 ? item.DiscountAmount / item.Quantity : 0;
                var applicableItemDiscounts = itemDiscounts.Where(d => d.RequiredProductId == item.ProductId || d.RequiredProductId == null).ToList();
                var applicableGiftAction = giftActions.FirstOrDefault(d => d.RequiredProductId == item.ProductId || d.RequiredProductId == null);

                decimal bestMonetaryDiscountPerUnit = 0m;
                Discount bestMonetaryDiscountInfo = null;

                foreach (var discount in applicableItemDiscounts)
                {
                    // Обрабатываем скидку "Сумма" здесь, применяя ее на всю позицию, если она выгоднее
                    if (discount.Type == "Сумма" && discount.Value.HasValue && discount.Value > 0)
                    {
                        decimal positionTotalBeforeDiscount = item.Quantity * item.PriceAtSale;
                        decimal potentialDiscountAmount = Math.Min(positionTotalBeforeDiscount, discount.Value.Value);
                        if (potentialDiscountAmount > bestMonetaryDiscountPerUnit * item.Quantity) // Сравниваем общую скидку
                        {
                            // Вычисляем эквивалентную скидку на единицу для сравнения и возможного применения
                            bestMonetaryDiscountPerUnit = item.Quantity > 0 ? potentialDiscountAmount / item.Quantity : 0;
                            bestMonetaryDiscountInfo = discount;
                        }
                    }
                    else // Процент или фикс. цена
                    {
                        decimal potentialDiscountPerUnit = CalculateSingleUnitPriceDiscount(item.PriceAtSale, discount);
                        if (potentialDiscountPerUnit > bestMonetaryDiscountPerUnit)
                        {
                            bestMonetaryDiscountPerUnit = potentialDiscountPerUnit;
                            bestMonetaryDiscountInfo = discount;
                        }
                    }
                }

                Product giftProduct = null;
                bool giftIsApplicable = false;
                if (applicableGiftAction?.GiftProductId != null)
                {
                    int giftId = applicableGiftAction.GiftProductId.Value;
                    if (!giftProductCache.TryGetValue(giftId, out giftProduct))
                    {
                        giftProduct = _productRepository.FindProductById(giftId);
                        giftProductCache[giftId] = giftProduct;
                    }
                    giftIsApplicable = giftProduct != null;
                }

                bool applyGift = giftIsApplicable;

                if (!applyGift && bestMonetaryDiscountInfo != null && bestMonetaryDiscountPerUnit > existingDiscountPerUnit)
                {
                    item.DiscountAmount = Math.Round(bestMonetaryDiscountPerUnit * item.Quantity, 2);
                    item.AppliedDiscountId = bestMonetaryDiscountInfo.DiscountId;
                }
                else if (applyGift && applicableGiftAction != null && giftProduct != null)
                {
                    item.AppliedDiscountId = applicableGiftAction.DiscountId;
                    giftsToAdd.Add(new CheckItem
                    {
                        ProductId = giftProduct.ProductId,
                        Quantity = 1 * item.Quantity,
                        PriceAtSale = 0,
                        ItemTotalAmount = 0,
                        DiscountAmount = 0,
                        AppliedDiscountId = applicableGiftAction.DiscountId
                    });
                }
            }
        }

        private static decimal CalculateSingleUnitPriceDiscount(decimal priceAtSale, Discount discount)
        {
            // Этот метод теперь считает только для % и фикс. цены
            decimal discountAmount = 0m;
            if (!discount.Value.HasValue || discount.Value.Value <= 0) return 0m;

            switch (discount.Type)
            {
                case "Процент":
                    discountAmount = priceAtSale * (Math.Min(100, discount.Value.Value) / 100m);
                    break;
                case "Фикс. цена":
                    if (discount.Value.Value < priceAtSale)
                        discountAmount = priceAtSale - discount.Value.Value;
                    break;
            }
            return Math.Max(0, discountAmount);
        }


        private static void GenerateNxMGifts(List<CheckItem> workingItems, List<Discount> activeDiscounts, List<CheckItem> giftsToAdd)
        {
            var nxmDiscounts = activeDiscounts.Where(d => d.Type == "N+M Подарок" && d.RequiredProductId.HasValue).ToList();
            if (!nxmDiscounts.Any()) return;

            var groupedByProduct = workingItems.GroupBy(i => i.ProductId);

            foreach (var group in groupedByProduct)
            {
                var rulesForThisProduct = nxmDiscounts.Where(r => r.RequiredProductId == group.Key).ToList();
                if (!rulesForThisProduct.Any()) continue;

                var discountRule = rulesForThisProduct.First();

                if (!discountRule.RequiredQuantityN.HasValue || discountRule.RequiredQuantityN.Value <= 0 ||
                    !discountRule.GiftQuantityM.HasValue || discountRule.GiftQuantityM.Value <= 0 ||
                    !discountRule.GiftProductId.HasValue) continue;

                decimal totalRequiredQuantity = group.Sum(i => i.Quantity);
                int n = discountRule.RequiredQuantityN.Value;
                int m = discountRule.GiftQuantityM.Value;

                int timesConditionMet = (int)Math.Floor(totalRequiredQuantity / n);
                if (timesConditionMet == 0) continue;

                int totalGiftsCount = timesConditionMet * m;

                Product giftProduct = _productRepository.FindProductById(discountRule.GiftProductId.Value);
                if (giftProduct == null)
                {
                    Debug.WriteLine($"Gift product ID {discountRule.GiftProductId.Value} for N+M discount not found.");
                    continue;
                }

                giftsToAdd.Add(new CheckItem
                {
                    ProductId = giftProduct.ProductId,
                    Quantity = totalGiftsCount,
                    PriceAtSale = 0,
                    ItemTotalAmount = 0,
                    DiscountAmount = 0,
                    AppliedDiscountId = discountRule.DiscountId
                });
            }
        }

        // Вспомогательный класс для возврата информации о лучшей скидке на чек
        private class CheckDiscountInfo
        {
            public Discount Discount { get; set; }
            public bool IsPercentage { get; set; }
            public decimal Value { get; set; }
            public decimal CalculatedAmount { get; set; }
        }

        private static CheckDiscountInfo FindBestCheckLevelDiscount(List<CheckItem> workingItems, List<Discount> activeDiscounts)
        {
            var checkDiscountRules = activeDiscounts.Where(d => d.Type == "Скидка на сумму чека").ToList();
            if (!checkDiscountRules.Any()) return null;

            decimal currentCheckTotal = workingItems.Sum(i => i.Quantity * i.PriceAtSale - i.DiscountAmount); // Сумма ПОСЛЕ скидок на позиции
            if (currentCheckTotal <= 0) return null;

            CheckDiscountInfo bestOffer = null;

            foreach (var discount in checkDiscountRules)
            {
                if (discount.CheckAmountThreshold.HasValue && currentCheckTotal >= discount.CheckAmountThreshold.Value)
                {
                    decimal potentialDiscountAmount = 0m;
                    decimal discountValue = discount.Value ?? 0m;
                    if (discountValue <= 0) continue;

                    bool isPercentage = discount.IsCheckDiscountPercentage;

                    if (isPercentage)
                    {
                        potentialDiscountAmount = currentCheckTotal * (Math.Min(100, discountValue) / 100m);
                    }
                    else
                    {
                        potentialDiscountAmount = Math.Min(currentCheckTotal, discountValue);
                    }

                    potentialDiscountAmount = Math.Round(potentialDiscountAmount, 2);

                    if (bestOffer == null || potentialDiscountAmount > bestOffer.CalculatedAmount)
                    {
                        bestOffer = new CheckDiscountInfo
                        {
                            Discount = discount,
                            IsPercentage = isPercentage,
                            Value = discountValue, // Сохраняем значение скидки (процент или сумма)
                            CalculatedAmount = potentialDiscountAmount // Сохраняем РАССЧИТАННУЮ сумму скидки
                        };
                    }
                }
            }
            return bestOffer;
        }

        private static void ApplyCheckLevelDistribution(List<CheckItem> workingItems, DiscountCalculationResult result)
        {
            if (result.AppliedCheckDiscount == null || !result.CheckDiscountValue.HasValue) return;

            decimal totalCheckDiscountToApply = 0m;
            decimal currentCheckTotal = workingItems.Sum(i => i.Quantity * i.PriceAtSale - i.DiscountAmount); // Сумма до скидки на чек
            if (currentCheckTotal <= 0) return;

            // Рассчитываем сумму скидки на чек на основе сохраненного значения
            if (result.IsCheckDiscountPercentage)
            {
                totalCheckDiscountToApply = currentCheckTotal * (Math.Min(100, result.CheckDiscountValue.Value) / 100m);
            }
            else
            {
                totalCheckDiscountToApply = Math.Min(currentCheckTotal, result.CheckDiscountValue.Value);
            }
            totalCheckDiscountToApply = Math.Round(totalCheckDiscountToApply, 2);

            if (totalCheckDiscountToApply <= 0) return;


            // Распределение
            decimal checkTotalForDistribution = workingItems.Sum(i => i.Quantity * i.PriceAtSale - i.DiscountAmount); // Сумма для долей
            if (checkTotalForDistribution <= 0) return;

            decimal distributedSum = 0m;
            // Распределяем только по тем позициям, где сумма положительная
            var itemsForDistribution = workingItems.Where(i => (i.Quantity * i.PriceAtSale - i.DiscountAmount) > 0).ToList();
            if (!itemsForDistribution.Any()) return;

            decimal totalAmountForDistribution = itemsForDistribution.Sum(i => i.Quantity * i.PriceAtSale - i.DiscountAmount);
            if (totalAmountForDistribution <= 0) return;


            for (int i = 0; i < itemsForDistribution.Count; i++)
            {
                var item = itemsForDistribution[i];
                decimal itemCurrentAmount = item.Quantity * item.PriceAtSale - item.DiscountAmount;

                decimal itemShare = itemCurrentAmount / totalAmountForDistribution;
                decimal discountPortion;

                if (i == itemsForDistribution.Count - 1)
                {
                    discountPortion = totalCheckDiscountToApply - distributedSum; // Остаток на последнюю позицию
                }
                else
                {
                    discountPortion = Math.Round(totalCheckDiscountToApply * itemShare, 2);
                }

                // Добавляем к существующей скидке
                decimal discountToAdd = Math.Max(0, Math.Min(itemCurrentAmount, discountPortion)); // Не больше, чем текущая сумма позиции

                if (discountToAdd > 0)
                {
                    item.DiscountAmount += discountToAdd;
                    item.AppliedDiscountId = result.AppliedCheckDiscount.DiscountId; // Перезаписываем ID
                    distributedSum += discountToAdd;
                }
            }
        }


        private static void RecalculateTotals(List<CheckItem> items)
        {
            foreach (var item in items)
            {
                // Убедимся, что скидка не превышает цену * кол-во
                decimal maxPossibleDiscount = item.Quantity * item.PriceAtSale;
                item.DiscountAmount = Math.Max(0, Math.Min(maxPossibleDiscount, item.DiscountAmount));
                // Пересчитываем итоговую сумму
                item.ItemTotalAmount = Math.Round(item.Quantity * item.PriceAtSale - item.DiscountAmount, 2);
            }
        }

        private static CheckItem CreateCleanCopy(CheckItem original)
        {
            return new CheckItem
            {
                ProductId = original.ProductId,
                Quantity = original.Quantity,
                PriceAtSale = original.PriceAtSale,
                DiscountAmount = 0m,
                AppliedDiscountId = null,
                ItemTotalAmount = Math.Round(original.Quantity * original.PriceAtSale, 2)
            };
        }

        public static decimal ApplyManualCheckDiscount(IList<CheckItem> checkItems, decimal discountValue, bool isPercentage)
        {
            if (checkItems == null || !checkItems.Any() || discountValue <= 0) return 0m;

            foreach (var item in checkItems)
            {
                item.DiscountAmount = 0m;
                item.AppliedDiscountId = null;
            }

            decimal totalCheckAmount = checkItems.Sum(i => i.Quantity * i.PriceAtSale);
            if (totalCheckAmount <= 0) return 0m;

            decimal totalDiscountToApply = 0m;

            if (isPercentage)
            {
                if (discountValue > 100) discountValue = 100;
                totalDiscountToApply = totalCheckAmount * (discountValue / 100m);
            }
            else
            {
                totalDiscountToApply = Math.Min(totalCheckAmount, discountValue);
            }
            totalDiscountToApply = Math.Round(totalDiscountToApply, 2);
            if (totalDiscountToApply <= 0) return 0m;

            decimal appliedSum = 0m;
            var itemsForDistribution = checkItems.Where(i => i.Quantity * i.PriceAtSale > 0).ToList();
            if (!itemsForDistribution.Any()) return 0m;
            decimal totalForDistribution = itemsForDistribution.Sum(i => i.Quantity * i.PriceAtSale);
            if (totalForDistribution <= 0) return 0m;


            for (int i = 0; i < itemsForDistribution.Count; i++)
            {
                var item = itemsForDistribution[i];
                decimal itemSubTotal = item.Quantity * item.PriceAtSale;
                decimal itemShare = (itemSubTotal / totalForDistribution);
                decimal discountPortion;

                if (i == itemsForDistribution.Count - 1) { discountPortion = totalDiscountToApply - appliedSum; }
                else { discountPortion = Math.Round(totalDiscountToApply * itemShare, 2); }

                item.DiscountAmount = Math.Max(0, Math.Min(itemSubTotal, discountPortion));
                appliedSum += item.DiscountAmount;
            }

            RecalculateTotals(checkItems.ToList());
            return Math.Round(appliedSum, 2);
        }
    }
}