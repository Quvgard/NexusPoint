using NexusPoint.Data.Repositories;
using NexusPoint.Models;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;

namespace NexusPoint.BusinessLogic
{
    public class CheckDisplayView : Check
    {
        public User Cashier { get; set; }
        public Shift Shift { get; set; }
    }

    public class DocumentPrintingService
    {
        private readonly CheckRepository _checkRepository;
        private readonly ShiftRepository _shiftRepository;
        private readonly UserRepository _userRepository;
        private readonly ProductRepository _productRepository;
        private readonly DiscountRepository _discountRepository;
        private readonly CultureInfo _culture = new CultureInfo("ru-RU");

        public DocumentPrintingService(
            CheckRepository checkRepository,
            ShiftRepository shiftRepository,
            UserRepository userRepository,
            ProductRepository productRepository,
            DiscountRepository discountRepository)
        {
            _checkRepository = checkRepository ?? throw new ArgumentNullException(nameof(checkRepository));
            _shiftRepository = shiftRepository ?? throw new ArgumentNullException(nameof(shiftRepository));
            _userRepository = userRepository ?? throw new ArgumentNullException(nameof(userRepository));
            _productRepository = productRepository ?? throw new ArgumentNullException(nameof(productRepository));
            _discountRepository = discountRepository ?? throw new ArgumentNullException(nameof(discountRepository));
        }
        public async Task<CheckDisplayView> FindCheckAsync(int checkNumber, int shiftNumber)
        {
            try
            {
                Check foundCheck = await Task.Run(() => _checkRepository.FindCheckByNumberAndShift(checkNumber, shiftNumber));
                if (foundCheck == null) return null;
                return await LoadCheckDetailsAsync(foundCheck);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка при поиске чека: {ex.Message}", "Ошибка поиска", MessageBoxButton.OK, MessageBoxImage.Error);
                return null;
            }
        }
        public async Task<CheckDisplayView> GetLastCheckAsync()
        {
            try
            {
                Check lastCheck = await Task.Run(() => _checkRepository.GetLastCheck());
                if (lastCheck == null) return null;

                return await LoadCheckDetailsAsync(lastCheck);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка при получении последнего чека: {ex.Message}", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
                return null;
            }
        }
        private async Task<CheckDisplayView> LoadCheckDetailsAsync(Check check)
        {
            if (check == null) return null;
            var cashierTask = Task.Run(() => _userRepository.GetUserById(check.UserId));
            var shiftTask = Task.Run(() => _shiftRepository.GetShiftById(check.ShiftId));
            if (check.Items == null || !check.Items.Any())
            {
                check.Items = await Task.Run(() => _checkRepository.GetCheckItemsByCheckId(check.CheckId));
            }


            await Task.WhenAll(cashierTask, shiftTask);

            var view = new CheckDisplayView
            {
                CheckId = check.CheckId,
                ShiftId = check.ShiftId,
                CheckNumber = check.CheckNumber,
                Timestamp = check.Timestamp,
                UserId = check.UserId,
                TotalAmount = check.TotalAmount,
                PaymentType = check.PaymentType,
                CashPaid = check.CashPaid,
                CardPaid = check.CardPaid,
                DiscountAmount = check.DiscountAmount,
                IsReturn = check.IsReturn,
                OriginalCheckId = check.OriginalCheckId,
                Items = check.Items ?? new List<CheckItem>(),
                Cashier = cashierTask.Result,
                Shift = shiftTask.Result
            };
            return view;
        }

        public async Task<string> FormatCheckCopyAsync(CheckDisplayView check)
        {
            if (check == null) return "Ошибка: Данные чека отсутствуют.";

            StringBuilder sb = new StringBuilder();
            sb.AppendLine("--- КОПИЯ ЧЕКА ---");
            sb.AppendLine($"ООО \"NexusPoint\"");
            sb.AppendLine($"Кассир: {check.Cashier?.FullName ?? "-"}");
            sb.AppendLine($"ИНН: 1234567890   ЗН ККТ: 00012345");
            sb.AppendLine($"Смена №: {check.Shift?.ShiftNumber ?? check.ShiftId}   Чек №: {check.CheckNumber}");
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
                sb.AppendLine($"  {item.Quantity} x {item.PriceAtSale.ToString("N2", _culture)} = {(item.Quantity * item.PriceAtSale).ToString("N2", _culture)}");
                if (item.DiscountAmount > 0) sb.AppendLine($"  Скидка: {item.DiscountAmount.ToString("N2", _culture)}");
                sb.AppendLine($"  ИТОГ ПО ПОЗИЦИИ: {item.ItemTotalAmount.ToString("N2", _culture)}");
            }
            sb.AppendLine("---------------------------------");
            sb.AppendLine($"ПОДЫТОГ: {(check.TotalAmount + check.DiscountAmount).ToString("N2", _culture)}");
            if (check.DiscountAmount > 0) sb.AppendLine($"СКИДКА НА ЧЕК: {check.DiscountAmount.ToString("N2", _culture)}");
            sb.AppendLine($"ИТОГО: {check.TotalAmount.ToString("C", _culture)}");
            sb.AppendLine("---------------------------------");
            string paymentTypeText = check.PaymentType == "Cash" ? "НАЛИЧНЫМИ" : check.PaymentType == "Card" ? "КАРТОЙ" : "СМЕШАННАЯ";
            sb.AppendLine($"ОПЛАТА ({paymentTypeText}): {check.TotalAmount.ToString("C", _culture)}");
            if (check.PaymentType == "Cash" || check.PaymentType == "Mixed") sb.AppendLine($"  ПОЛУЧЕНО НАЛ: {check.CashPaid.ToString("C", _culture)}");
            if (check.PaymentType == "Card" || check.PaymentType == "Mixed") sb.AppendLine($"  ПОЛУЧЕНО КАРТОЙ: {check.CardPaid.ToString("C", _culture)}");

            decimal change = (check.CashPaid + check.CardPaid) - check.TotalAmount;
            if (!check.IsReturn && change > 0.001m) sb.AppendLine($"СДАЧА: {change.ToString("C", _culture)}");

            sb.AppendLine("---------------------------------");
            sb.AppendLine($"ФН: 999900001111222   ФД: {check.CheckId + 10000}  ФП: 1234567890");
            sb.AppendLine("--- КОНЕЦ КОПИИ ---");

            return sb.ToString();
        }

        public async Task<string> FormatTovarnyCheckAsync(CheckDisplayView check)
        {
            if (check == null || check.IsReturn) return "Ошибка: Товарный чек печатается только для чеков продажи.";

            StringBuilder sb = new StringBuilder();
            sb.AppendLine($"--- ТОВАРНЫЙ ЧЕК № {check.CheckNumber} от {check.Timestamp:d} ---");
            sb.AppendLine($"Продавец: ООО \"NexusPoint\"");
            sb.AppendLine($"Кассир: {check.Cashier?.FullName ?? "-"}");
            sb.AppendLine("--------------------------------------------------");
            sb.AppendLine("| № | Наименование товара          | Кол-во | Цена  | Сумма |");
            sb.AppendLine("--------------------------------------------------");

            var productIds = check.Items.Select(i => i.ProductId).Distinct().ToList();
            var products = (await Task.Run(() => _productRepository.GetProductsByIds(productIds)))
                           .ToDictionary(p => p.ProductId);
            int index = 1;
            foreach (var item in check.Items)
            {
                string productName = products.TryGetValue(item.ProductId, out Product p) ? p.Name : "<Товар?>";
                sb.AppendFormat("|{0,3}| {1,-28}|{2,8}|{3,7}|{4,7}|\n",
                               index++, productName.Length > 28 ? productName.Substring(0, 28) : productName,
                               item.Quantity, item.PriceAtSale.ToString("N2", _culture), item.ItemTotalAmount.ToString("N2", _culture));
            }
            sb.AppendLine("--------------------------------------------------");
            sb.AppendLine($"Всего наименований: {check.Items.Count}, на сумму: {check.TotalAmount.ToString("C", _culture)}");
            if (check.DiscountAmount > 0) sb.AppendLine($"В том числе скидка: {check.DiscountAmount.ToString("N2", _culture)} руб.");
            sb.AppendLine("\nПодпись кассира: __________________");

            return sb.ToString();
        }

        public async Task<string> FormatDiscountDetailsAsync(CheckDisplayView check)
        {
            if (check == null) return "Ошибка: Данные чека отсутствуют.";

            StringBuilder sb = new StringBuilder();
            sb.AppendLine($"--- Расшифровка скидок к чеку №{check.CheckNumber} ---");

            if (check.DiscountAmount <= 0 || check.Items == null || !check.Items.Any())
            {
                sb.AppendLine("Скидки к данному чеку не применялись.");
                return sb.ToString();
            }

            var appliedDiscountIds = check.Items.Where(i => i.AppliedDiscountId.HasValue).Select(i => i.AppliedDiscountId.Value).Distinct().ToList();
            Dictionary<int, Discount> discountsInfo = new Dictionary<int, Discount>();
            if (appliedDiscountIds.Any())
            {
                try
                {
                    discountsInfo = (await Task.Run(() => _discountRepository.GetDiscountsByIds(appliedDiscountIds))).ToDictionary(d => d.DiscountId);
                }
                catch (Exception ex) { sb.AppendLine($"(Ошибка загрузки названий скидок: {ex.Message})"); }
            }

            var productIds = check.Items.Select(i => i.ProductId).Distinct().ToList();
            var productsInfo = (await Task.Run(() => _productRepository.GetProductsByIds(productIds))).ToDictionary(p => p.ProductId);


            sb.AppendLine("Примененные акции:");
            bool detailsFound = false;
            foreach (var item in check.Items)
            {
                if (item.AppliedDiscountId.HasValue && item.DiscountAmount > 0)
                {
                    string discountName = discountsInfo.TryGetValue(item.AppliedDiscountId.Value, out Discount discount) ? discount.Name : $"<Скидка ID: {item.AppliedDiscountId.Value}>";
                    string productName = productsInfo.TryGetValue(item.ProductId, out Product product) ? product.Name : "<Товар?>";

                    sb.AppendLine($"- К товару '{productName}': Акция '{discountName}' (Общая скидка на позицию: {item.DiscountAmount:C})");
                    detailsFound = true;
                }
            }
            decimal itemDiscountsSum = check.Items.Where(i => i.AppliedDiscountId.HasValue).Sum(i => i.DiscountAmount);
            decimal checkLevelDiscount = check.DiscountAmount - itemDiscountsSum;

            if (checkLevelDiscount > 0.001m)
            {
                int? checkDiscountId = check.Items.FirstOrDefault(i => i.AppliedDiscountId.HasValue && discountsInfo.ContainsKey(i.AppliedDiscountId.Value) && discountsInfo[i.AppliedDiscountId.Value].Type == "Скидка на сумму чека")?.AppliedDiscountId;
                string checkDiscountName = "<Общая скидка на чек>";
                if (checkDiscountId.HasValue && discountsInfo.TryGetValue(checkDiscountId.Value, out Discount cd))
                {
                    checkDiscountName = $"Акция '{cd.Name}'";
                }
                sb.AppendLine($"- {checkDiscountName} (Сумма: {checkLevelDiscount:C})");
                detailsFound = true;
            }


            if (!detailsFound && check.DiscountAmount > 0)
            {
                sb.AppendLine($"(Общая скидка на чек: {check.DiscountAmount:C}, без детализации по акциям)");
            }

            sb.AppendLine("\nОбщая сумма скидки по чеку: " + check.DiscountAmount.ToString("C", _culture));
            return sb.ToString();
        }
    }
}