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
    // --- Вспомогательный класс для хранения результата расчета скидок ---
    public class DiscountCalculationResult
    {
        public decimal TotalDiscountAmount { get; set; } = 0m;
        // Список позиций с примененными скидками (модифицированные DiscountAmount/AppliedDiscountId)
        public List<CheckItem> DiscountedItems { get; set; } = new List<CheckItem>();
        // Список подарков, которые нужно добавить в чек (новый CheckItem с ценой 0 или акционной)
        public List<CheckItem> GiftsToAdd { get; set; } = new List<CheckItem>();
        // Информация о примененной скидке на чек (если была)
        public Discount AppliedCheckDiscount { get; set; } = null;
    }

    public static class DiscountCalculator
    {
        // Используем статические экземпляры репозиториев для простоты.
        // В больших приложениях лучше использовать Dependency Injection.
        private static readonly DiscountRepository _discountRepository = new DiscountRepository();
        private static readonly ProductRepository _productRepository = new ProductRepository();

        /// <summary>
        /// Основной метод для применения всех автоматических скидок к набору позиций чека.
        /// Обрабатывает скидки на позиции, скидки на N-ный товар, акции N+M подарок,
        /// акцию "Подарок", и скидки на сумму чека.
        /// Применяет правило "самой выгодной" скидки на единицу товара, где это применимо.
        /// </summary>
        /// <param name="originalCheckItems">Коллекция исходных позиций чека.</param>
        /// <returns>Объект DiscountCalculationResult с результатами.</returns>
        public static DiscountCalculationResult ApplyAllAutoDiscounts(IEnumerable<CheckItem> originalCheckItems)
        {
            var result = new DiscountCalculationResult();
            if (originalCheckItems == null || !originalCheckItems.Any()) return result;

            List<Discount> activeDiscounts;
            try
            {
                // Загружаем все активные на данный момент акции
                activeDiscounts = _discountRepository.GetAllActiveDiscounts().ToList();
                // Если активных акций нет, просто возвращаем исходные данные без изменений
                if (!activeDiscounts.Any())
                {
                    result.DiscountedItems = originalCheckItems.Select(CreateCleanCopy).ToList();
                    RecalculateTotals(result.DiscountedItems); // Пересчитываем суммы на всякий случай
                    return result;
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error loading active discounts: {ex.Message}");
                MessageBox.Show($"Не удалось загрузить активные скидки: {ex.Message}. Скидки не будут применены.", "Ошибка скидок", MessageBoxButton.OK, MessageBoxImage.Warning);
                result.DiscountedItems = originalCheckItems.Select(CreateCleanCopy).ToList(); // Возвращаем без скидок
                RecalculateTotals(result.DiscountedItems);
                return result; // Возвращаем пустой результат при ошибке
            }

            // --- Создаем рабочую копию позиций чека ---
            // Работаем с копиями, чтобы не изменять исходные объекты напрямую
            // и чтобы сбросить предыдущие расчеты скидок.
            var workingItems = originalCheckItems.Select(CreateCleanCopy).ToList();

            // --- Этап 1: Применение скидок, действующих на КОНКРЕТНЫЕ ЕДИНИЦЫ ТОВАРА ---
            // Сюда входят "Скидка на N-ный" и потенциально другие будущие типы.
            // Они модифицируют DiscountAmount у отдельных единиц товара внутри позиций.
            ApplyNthItemDiscounts(workingItems, activeDiscounts);

            // --- Этап 2: Определение и применение ЛУЧШЕЙ скидки/акции для каждой ЕДИНИЦЫ товара ---
            // На этом этапе конкурируют: "Процент", "Сумма", "Фикс. цена" и акция "Подарок".
            // Также учитываются скидки, уже примененные на этапе 1 (N-ный).
            // Мы проходим по каждой единице каждого товара.
            ApplyBestOfferPerUnit(workingItems, activeDiscounts, result.GiftsToAdd);

            // --- Этап 3: Определение и генерация подарков по акции "N+M Подарок" ---
            // Эта акция не конкурирует с другими за единицу товара, она просто добавляет подарки,
            // если условие по количеству основного товара выполнено.
            // Важно выполнять ПОСЛЕ применения скидок на основные товары, т.к. N+M может
            // зависеть от количества НЕ скидочных товаров (если такая логика нужна).
            // Текущая реализация проще - зависит от общего количества.
            GenerateNxMGifts(workingItems, activeDiscounts, result.GiftsToAdd);

            // --- Этап 4: Применение скидки на ВЕСЬ ЧЕК ---
            // Находит лучшую скидку типа "Скидка на сумму чека" и распределяет ее
            // по позициям, УЖЕ содержащим скидки предыдущих этапов.
            result.AppliedCheckDiscount = ApplyCheckLevelDiscounts(workingItems, activeDiscounts);

            // --- Финальный расчет ---
            RecalculateTotals(workingItems); // Финальный пересчет ItemTotalAmount
            result.DiscountedItems = workingItems; // Записываем итоговый список позиций
            result.TotalDiscountAmount = result.DiscountedItems.Sum(i => i.DiscountAmount); // Считаем общую сумму скидки

            Debug.WriteLine($"Discount calculation finished. Total Discount: {result.TotalDiscountAmount:C}");
            return result;
        }

        // --- Приватные методы для этапов расчета ---

        /// <summary>
        /// Применяет скидки типа "Скидка на N-ный".
        /// Модифицирует DiscountAmount и AppliedDiscountId позиций в workingItems.
        /// </summary>
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

                // TODO: Определить, какая N-ная скидка выгоднее, если их несколько на один товар. Пока берем первую.
                var discountRule = rulesForThisProduct.First(); // Упрощение!

                if (!discountRule.NthItemNumber.HasValue || discountRule.NthItemNumber.Value <= 0 ||
                    !discountRule.Value.HasValue || discountRule.Value.Value <= 0) continue;

                int n = discountRule.NthItemNumber.Value;
                decimal totalQuantity = itemsInGroup.Sum(i => i.Quantity);
                int nthItemsCount = (int)Math.Floor(totalQuantity / n); // Целое число N-ных товаров

                if (nthItemsCount == 0) continue;

                decimal priceOfOneUnit = itemsInGroup.First().PriceAtSale; // Предполагаем, что цена одинакова для одного ProductId
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

                // Распределяем скидку по N-ным единицам, проходя по позициям
                decimal quantityCounter = 0;
                int nthAppliedCount = 0;
                foreach (var item in itemsInGroup.OrderBy(i => workingItems.IndexOf(i))) // Обрабатываем в порядке их появления в чеке
                {
                    decimal itemStartQty = quantityCounter;
                    decimal itemEndQty = quantityCounter + item.Quantity;
                    decimal itemDiscountApplied = 0m; // Скидка, примененная к этой позиции

                    // Определяем, сколько N-ных единиц попадает в диапазон этой позиции
                    int firstNthIndexInItem = (int)Math.Ceiling((itemStartQty + 0.0001m) / n); // Индекс первого N-ного товара >= начала позиции
                    int lastNthIndexInItem = (int)Math.Floor(itemEndQty / n);          // Индекс последнего N-ного товара <= конца позиции

                    int nthCountInItem = Math.Max(0, lastNthIndexInItem - firstNthIndexInItem + 1);

                    // Ограничиваем количество применяемых скидок общим числом nthItemsCount
                    nthCountInItem = Math.Min(nthCountInItem, nthItemsCount - nthAppliedCount);


                    if (nthCountInItem > 0)
                    {
                        itemDiscountApplied = discountPerNthUnit * nthCountInItem;
                        item.DiscountAmount += itemDiscountApplied; // Добавляем к скидке позиции
                        item.AppliedDiscountId = discountRule.DiscountId; // Помечаем, какая скидка сработала
                        nthAppliedCount += nthCountInItem;
                        Debug.WriteLine($"Applied Nth discount ({discountPerNthUnit:C} x {nthCountInItem}) to item {item.ProductId}. Total applied: {nthAppliedCount}/{nthItemsCount}");

                    }

                    quantityCounter = itemEndQty;
                    if (nthAppliedCount >= nthItemsCount) break; // Все скидки для этой группы применены
                }
            }
        }

        /// <summary>
        /// Применяет лучшую денежную скидку или акцию "Подарок" для каждой единицы товара.
        /// Модифицирует DiscountAmount/AppliedDiscountId в workingItems и добавляет подарки в giftsToAdd.
        /// </summary>
        private static void ApplyBestOfferPerUnit(List<CheckItem> workingItems, List<Discount> activeDiscounts, List<CheckItem> giftsToAdd)
        {
            var itemDiscountTypes = new[] { "Процент", "Сумма", "Фикс. цена" };
            var itemDiscounts = activeDiscounts.Where(d => itemDiscountTypes.Contains(d.Type)).ToList();
            var giftActions = activeDiscounts.Where(d => d.Type == "Подарок").ToList();

            // Кэш для информации о подарках, чтобы не загружать много раз
            var giftProductCache = new Dictionary<int, Product>();

            foreach (var item in workingItems)
            {
                // Скидка, уже примененная на предыдущих этапах (например, N-ный)
                decimal existingDiscountPerUnit = item.Quantity > 0 ? item.DiscountAmount / item.Quantity : 0;

                // Ищем применимые скидки на эту позицию
                var applicableItemDiscounts = itemDiscounts
                    .Where(d => d.RequiredProductId == item.ProductId || d.RequiredProductId == null)
                    .ToList();
                var applicableGiftAction = giftActions
                    .FirstOrDefault(d => d.RequiredProductId == item.ProductId || d.RequiredProductId == null);

                decimal bestMonetaryDiscountPerUnit = 0m;
                Discount bestMonetaryDiscountInfo = null;

                // Находим лучшую денежную скидку на ЕДИНИЦУ товара
                foreach (var discount in applicableItemDiscounts)
                {
                    decimal potentialDiscountPerUnit = CalculateSingleUnitPriceDiscount(item.PriceAtSale, discount);
                    if (potentialDiscountPerUnit > bestMonetaryDiscountPerUnit)
                    {
                        bestMonetaryDiscountPerUnit = potentialDiscountPerUnit;
                        bestMonetaryDiscountInfo = discount;
                    }
                }

                // Проверяем акцию "Подарок"
                Product giftProduct = null;
                bool giftIsApplicable = false;
                if (applicableGiftAction?.GiftProductId != null)
                {
                    int giftId = applicableGiftAction.GiftProductId.Value;
                    if (!giftProductCache.TryGetValue(giftId, out giftProduct))
                    {
                        giftProduct = _productRepository.FindProductById(giftId);
                        giftProductCache[giftId] = giftProduct; // Кэшируем (даже если null)
                    }
                    giftIsApplicable = giftProduct != null;
                }

                // --- Логика выбора: Подарок или Денежная скидка? ---
                // Правило: Применяем то, что выгоднее для клиента, если есть выбор.
                // Выгода подарка = его обычная цена. Выгода скидки = рассчитанная сумма.
                // ИЛИ: Правило магазина (например, подарок всегда приоритетнее).
                // Примем правило: Подарок приоритетнее, если он применим.

                bool applyGift = giftIsApplicable; // Подарок приоритетнее

                // Если подарок не применяется, применяем лучшую денежную скидку (если она больше уже существующей)
                if (!applyGift && bestMonetaryDiscountInfo != null && bestMonetaryDiscountPerUnit > existingDiscountPerUnit)
                {
                    item.DiscountAmount = Math.Round(bestMonetaryDiscountPerUnit * item.Quantity, 2); // Общая скидка на позицию
                    item.AppliedDiscountId = bestMonetaryDiscountInfo.DiscountId;
                }
                // Если применяется подарок, денежную скидку этого этапа НЕ добавляем,
                // но оставляем скидку от N-ного, если она была.
                // Помечаем позицию ID акции-подарка.
                else if (applyGift && applicableGiftAction != null && giftProduct != null)
                {
                    // Не меняем item.DiscountAmount (оставляем скидку от N-ного, если была)
                    item.AppliedDiscountId = applicableGiftAction.DiscountId; // Помечаем ID акции Подарок

                    // Добавляем подарок в список на добавление
                    giftsToAdd.Add(new CheckItem
                    {
                        ProductId = giftProduct.ProductId,
                        Quantity = 1 * item.Quantity, // 1 подарок за каждую единицу товара-условия
                        PriceAtSale = 0,
                        ItemTotalAmount = 0,
                        DiscountAmount = 0,
                        AppliedDiscountId = applicableGiftAction.DiscountId // Связь
                    });
                }
                // Если ни подарок, ни выгодная денежная скидка не найдены, оставляем скидку от N-ного (если была).
            }
        }

        /// <summary>
        /// Рассчитывает ПОТЕНЦИАЛЬНУЮ сумму скидки для ОДНОЙ ЕДИНИЦЫ товара.
        /// </summary>
        private static decimal CalculateSingleUnitPriceDiscount(decimal priceAtSale, Discount discount)
        {
            decimal discountAmount = 0m;
            if (!discount.Value.HasValue || discount.Value.Value <= 0) return 0m;

            switch (discount.Type)
            {
                case "Процент":
                    discountAmount = priceAtSale * (Math.Min(100, discount.Value.Value) / 100m);
                    break;
                case "Сумма":
                    // Важно: Скидка суммой на ПОЗИЦИЮ. Здесь считаем на ЕДИНИЦУ.
                    // Если акция "50р на товар", она применяется один раз на позицию.
                    // Эта логика здесь не совсем корректна для "Сумма".
                    // Перенесём логику "Сумма" на позицию в ApplyBestOfferPerUnit?
                    // Пока оставим 0, т.к. "Сумма" обычно на позицию, а не единицу.
                    discountAmount = 0; // TODO: Уточнить логику скидки "Сумма" - на единицу или позицию?
                    break;
                case "Фикс. цена":
                    if (discount.Value.Value < priceAtSale)
                        discountAmount = priceAtSale - discount.Value.Value;
                    break;
            }
            return Math.Max(0, discountAmount);
        }

        /// <summary>
        /// Генерирует подарки по акции N+M. Добавляет их в giftsToAdd.
        /// </summary>
        private static void GenerateNxMGifts(List<CheckItem> workingItems, List<Discount> activeDiscounts, List<CheckItem> giftsToAdd)
        {
            var nxmDiscounts = activeDiscounts.Where(d => d.Type == "N+M Подарок" && d.RequiredProductId.HasValue).ToList();
            if (!nxmDiscounts.Any()) return;

            var groupedByProduct = workingItems.GroupBy(i => i.ProductId);

            foreach (var group in groupedByProduct)
            {
                var rulesForThisProduct = nxmDiscounts.Where(r => r.RequiredProductId == group.Key).ToList();
                if (!rulesForThisProduct.Any()) continue;

                // TODO: Если несколько N+M акций на один товар, выбрать лучшую? Пока берем первую.
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

                // Помечаем исходные позиции? Не обязательно.
            }
        }


        /// <summary>
        /// Находит лучшую скидку на чек и распределяет ее по позициям.
        /// Модифицирует DiscountAmount и AppliedDiscountId позиций.
        /// </summary>
        /// <returns>Примененная скидка на чек или null.</returns>
        private static Discount ApplyCheckLevelDiscounts(List<CheckItem> workingItems, List<Discount> activeDiscounts)
        {
            var checkDiscountRules = activeDiscounts.Where(d => d.Type == "Скидка на сумму чека").ToList();
            if (!checkDiscountRules.Any()) return null;

            // Сумма чека до применения скидки на чек
            decimal currentCheckTotal = workingItems.Sum(i => i.ItemTotalAmount); // Используем уже рассчитанную сумму с учетом скидок на позиции
            if (currentCheckTotal <= 0) return null;

            Discount bestCheckDiscountInfo = null;
            decimal bestCheckDiscountAmount = 0m;

            // Ищем лучшую скидку на чек
            foreach (var discount in checkDiscountRules)
            {
                if (discount.CheckAmountThreshold.HasValue && currentCheckTotal >= discount.CheckAmountThreshold.Value)
                {
                    decimal potentialDiscount = 0m;
                    if (discount.Value.HasValue && discount.Value.Value > 0)
                    {
                        if (discount.IsCheckDiscountPercentage)
                        {
                            potentialDiscount = currentCheckTotal * (Math.Min(100, discount.Value.Value) / 100m);
                        }
                        else
                        {
                            potentialDiscount = Math.Min(currentCheckTotal, discount.Value.Value);
                        }
                    }
                    if (potentialDiscount > bestCheckDiscountAmount)
                    {
                        bestCheckDiscountAmount = potentialDiscount;
                        bestCheckDiscountInfo = discount;
                    }
                }
            }

            // Распределяем найденную скидку
            if (bestCheckDiscountInfo != null && bestCheckDiscountAmount > 0)
            {
                decimal totalCheckDiscountToApply = Math.Round(bestCheckDiscountAmount, 2);
                decimal checkTotalForDistribution = workingItems.Sum(i => i.ItemTotalAmount); // Сумма для расчета долей
                if (checkTotalForDistribution <= 0) return bestCheckDiscountInfo; // Не распределяем на нулевую сумму

                decimal distributedSum = 0m;
                for (int i = 0; i < workingItems.Count; i++)
                {
                    var item = workingItems[i];
                    if (item.ItemTotalAmount <= 0) continue; // Не распределяем на позиции с нулевой или отрицательной суммой

                    decimal itemShare = item.ItemTotalAmount / checkTotalForDistribution;
                    decimal discountPortion;

                    if (i == workingItems.Count - 1)
                    {
                        discountPortion = totalCheckDiscountToApply - distributedSum;
                    }
                    else
                    {
                        discountPortion = Math.Round(totalCheckDiscountToApply * itemShare, 2);
                    }

                    // Добавляем к существующей скидке, не превышая остаток суммы
                    decimal maxPossibleDiscountAdd = item.ItemTotalAmount;
                    decimal discountToAdd = Math.Max(0, Math.Min(maxPossibleDiscountAdd, discountPortion));

                    item.DiscountAmount += discountToAdd;
                    // AppliedDiscountId перезаписывается на ID скидки чека
                    if (discountToAdd > 0) // Применяем ID только если скидка реально добавилась
                    {
                        item.AppliedDiscountId = bestCheckDiscountInfo.DiscountId;
                    }

                    distributedSum += discountToAdd;
                }
                // Коррекция округления (если нужна) - можно добавить
            }
            return bestCheckDiscountInfo; // Возвращаем примененную скидку
        }

        // --- Вспомогательные ---

        private static void RecalculateTotals(List<CheckItem> items)
        {
            foreach (var item in items)
            {
                item.DiscountAmount = Math.Max(0, Math.Min(item.Quantity * item.PriceAtSale, item.DiscountAmount));
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
                DiscountAmount = 0m, // Сброс
                AppliedDiscountId = null, // Сброс
                ItemTotalAmount = Math.Round(original.Quantity * original.PriceAtSale, 2) // Начальная сумма
            };
        }

        // --- ВОССТАНАВЛИВАЕМ МЕТОД ДЛЯ РУЧНОЙ СКИДКИ ---
        /// <summary>
        /// Применяет ручную скидку (процент или сумма) ко всему чеку,
        /// пропорционально распределяя ее по позициям.
        /// Модифицирует DiscountAmount элементов списка. НЕ ИЗМЕНЯЕТ AppliedDiscountId.
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

            // Сбрасываем ТОЛЬКО ручные скидки (если нужно различать)
            // Но проще пока сбросить все скидки перед применением ручной
            // (вызывающий код должен решить - применять авто или ручную)
            // Либо можно добавлять ручную поверх автоматической? Зависит от правил.
            // Пока будем считать, что ручная ПЕРЕЗАПИСЫВАЕТ автоматические.
            foreach (var item in checkItems)
            {
                item.DiscountAmount = 0m;
                item.AppliedDiscountId = null; // Ручная скидка не имеет ID акции
            }


            decimal totalCheckAmount = checkItems.Sum(i => i.Quantity * i.PriceAtSale); // Сумма чека ДО скидок
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

            decimal appliedSum = 0m;
            var itemsForDistribution = checkItems.Where(i => i.Quantity * i.PriceAtSale > 0).ToList(); // Распределяем только на позиции с ненулевой суммой
            if (!itemsForDistribution.Any()) return 0m; // Некуда распределять
            decimal totalForDistribution = itemsForDistribution.Sum(i => i.Quantity * i.PriceAtSale);
            if (totalForDistribution <= 0) return 0m;


            for (int i = 0; i < itemsForDistribution.Count; i++)
            {
                var item = itemsForDistribution[i];
                decimal itemSubTotal = item.Quantity * item.PriceAtSale;
                decimal itemShare = (itemSubTotal / totalForDistribution);
                decimal discountPortion;

                if (i == itemsForDistribution.Count - 1)
                {
                    discountPortion = totalDiscountToApply - appliedSum;
                }
                else
                {
                    discountPortion = Math.Round(totalDiscountToApply * itemShare, 2);
                }

                // Убедимся, что скидка на позицию не больше ее суммы
                item.DiscountAmount = Math.Max(0, Math.Min(itemSubTotal, discountPortion));
                appliedSum += item.DiscountAmount;
            }

            // Корректировка округления (можно добавить, как раньше)


            // Пересчитываем ItemTotalAmount после применения ручной скидки
            RecalculateTotals(checkItems.ToList()); // Передаем весь список

            return Math.Round(appliedSum, 2);
        }
        // --- КОНЕЦ ВОССТАНОВЛЕННОГО МЕТОДА ---

    } // Конец класса DiscountCalculator
}