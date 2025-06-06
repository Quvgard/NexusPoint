using NexusPoint.Data.Repositories;
using NexusPoint.Models;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NexusPoint.BusinessLogic
{
    public class ReportService
    {
        private readonly CheckRepository _checkRepository;
        private readonly CashDrawerOperationRepository _cashDrawerRepository;
        private readonly UserRepository _userRepository;
        private readonly CultureInfo _culture = new CultureInfo("ru-RU"); // Для форматирования

        public ReportService(CheckRepository checkRepository, CashDrawerOperationRepository cashDrawerRepository, UserRepository userRepository)
        {
            _checkRepository = checkRepository ?? throw new ArgumentNullException(nameof(checkRepository));
            _cashDrawerRepository = cashDrawerRepository ?? throw new ArgumentNullException(nameof(cashDrawerRepository));
            _userRepository = userRepository ?? throw new ArgumentNullException(nameof(userRepository));
        }

        public async Task<string> GenerateXReportAsync(Shift shift)
        {
            if (shift == null) return "Ошибка: Данные смены не предоставлены.";

            var checksTask = Task.Run(() => _checkRepository.GetChecksByShiftId(shift.ShiftId).ToList());
            var cashOpsTask = Task.Run(() => _cashDrawerRepository.GetOperationsByShiftId(shift.ShiftId).ToList());
            var cashierTask = Task.Run(() => _userRepository.GetUserById(shift.OpeningUserId));

            await Task.WhenAll(checksTask, cashOpsTask, cashierTask);

            var checks = checksTask.Result;
            var cashOps = cashOpsTask.Result;
            var openingCashier = cashierTask.Result;

            decimal totalSales = checks.Where(c => !c.IsReturn).Sum(c => c.TotalAmount);
            decimal totalReturns = checks.Where(c => c.IsReturn).Sum(c => c.TotalAmount);
            decimal cashSales = checks.Where(c => !c.IsReturn).Sum(c => c.PaymentType == "Cash" ? c.TotalAmount : c.PaymentType == "Mixed" ? c.CashPaid : 0);
            decimal cardSales = checks.Where(c => !c.IsReturn).Sum(c => c.PaymentType == "Card" ? c.TotalAmount : c.PaymentType == "Mixed" ? c.CardPaid : 0);

            // --- ИСПРАВЛЕННАЯ СТРОКА ---
            decimal cashReturns = checks.Where(c => c.IsReturn).Sum(c => CalculateCashReturned(c));
            // --- КОНЕЦ ИСПРАВЛЕНИЯ ---

            decimal cashAdded = cashOps.Where(co => co.OperationType == "CashIn").Sum(co => co.Amount);
            decimal cashRemoved = cashOps.Where(co => co.OperationType == "CashOut").Sum(co => co.Amount);
            decimal currentCashTheoretic = shift.StartCash + cashSales + cashAdded - cashRemoved - cashReturns;

            StringBuilder sb = new StringBuilder();
            sb.AppendLine($"--- X-Отчет (Промежуточный) ---");
            sb.AppendLine($"Смена №: {shift.ShiftNumber}");
            sb.AppendLine($"Открыта: {shift.OpenTimestamp:G}");
            sb.AppendLine($"Текущее время: {DateTime.Now:G}");
            sb.AppendLine($"Кассир: {openingCashier?.FullName ?? "-"} (Открыл)");
            sb.AppendLine($"---------------------------------");
            sb.AppendLine($"Начальный остаток нал.: {shift.StartCash.ToString("C", _culture)}");
            sb.AppendLine($"Внесения: {cashAdded.ToString("C", _culture)}");
            sb.AppendLine($"Изъятия: {cashRemoved.ToString("C", _culture)}");
            sb.AppendLine($"---------------------------------");
            sb.AppendLine($"Продажи (Итог): {totalSales.ToString("C", _culture)}");
            sb.AppendLine($"  в т.ч. наличными: {cashSales.ToString("C", _culture)}");
            sb.AppendLine($"  в т.ч. картой: {cardSales.ToString("C", _culture)}");
            sb.AppendLine($"Возвраты (Итог): {totalReturns.ToString("C", _culture)}");
            sb.AppendLine($"  (возвращено наличными: {cashReturns.ToString("C", _culture)})");
            sb.AppendLine($"---------------------------------");
            sb.AppendLine($"Наличных в кассе (теор.): {currentCashTheoretic.ToString("C", _culture)}");
            sb.AppendLine($"=================================");
            sb.AppendLine("(Отчет без гашения)");

            return sb.ToString();
        }

        public async Task<string> GenerateZReportAsync(Shift closedShift)
        {
            if (closedShift == null || !closedShift.IsClosed || !closedShift.CloseTimestamp.HasValue)
            {
                return "Ошибка: Для Z-отчета нужна закрытая смена.";
            }

            User openingCashier = null;
            User closingCashier = null;

            var openingUserTask = Task.Run(() => _userRepository.GetUserById(closedShift.OpeningUserId));
            var closingUserTask = closedShift.ClosingUserId.HasValue
                ? Task.Run(() => _userRepository.GetUserById(closedShift.ClosingUserId.Value))
                : Task.FromResult<User>(null);

            await Task.WhenAll(openingUserTask, closingUserTask);
            openingCashier = openingUserTask.Result;
            closingCashier = closingUserTask.Result;


            StringBuilder sb = new StringBuilder();
            sb.AppendLine($"--- Z-Отчет (Гашение) ---");
            sb.AppendLine($"Смена №: {closedShift.ShiftNumber}");
            sb.AppendLine($"Открыта: {closedShift.OpenTimestamp:G}");
            sb.AppendLine($"Закрыта: {closedShift.CloseTimestamp.Value:G}");
            sb.AppendLine($"Кассир откр.: {openingCashier?.FullName ?? "-"} (ID: {closedShift.OpeningUserId})");
            sb.AppendLine($"Кассир закр.: {closingCashier?.FullName ?? "-"} (ID: {closedShift.ClosingUserId?.ToString() ?? "-"})");
            sb.AppendLine($"---------------------------------");
            sb.AppendLine($"Начальный остаток: {closedShift.StartCash.ToString("C", _culture)}");
            sb.AppendLine($"Внесения: {(closedShift.CashAdded ?? 0).ToString("C", _culture)}");
            sb.AppendLine($"Изъятия: {(closedShift.CashRemoved ?? 0).ToString("C", _culture)}");
            sb.AppendLine($"---------------------------------");
            sb.AppendLine($"Продажи (Итог): {(closedShift.TotalSales ?? 0).ToString("C", _culture)}");
            sb.AppendLine($"  Наличными: {(closedShift.CashSales ?? 0).ToString("C", _culture)}");
            sb.AppendLine($"  Картой: {(closedShift.CardSales ?? 0).ToString("C", _culture)}");
            sb.AppendLine($"Возвраты (Итог): {(closedShift.TotalReturns ?? 0).ToString("C", _culture)}");
            // Пересчитаем возвращенные наличные для Z-отчета тоже, т.к. они не хранятся
            var checksInShift = await Task.Run(() => _checkRepository.GetChecksByShiftId(closedShift.ShiftId).ToList());
            decimal cashReturnsInShift = checksInShift.Where(c => c.IsReturn).Sum(c => CalculateCashReturned(c));
            sb.AppendLine($"  (Возвращено наличными: {cashReturnsInShift.ToString("C", _culture)})");
            sb.AppendLine($"---------------------------------");
            sb.AppendLine($"Наличных в кассе (теор.): {(closedShift.EndCashTheoretic ?? 0).ToString("C", _culture)}");
            sb.AppendLine($"Наличных в кассе (факт.): {(closedShift.EndCashActual ?? 0).ToString("C", _culture)}");
            sb.AppendLine($"Расхождение: {(closedShift.Difference ?? 0).ToString("C", _culture)}");
            sb.AppendLine($"=================================");

            return sb.ToString();
        }

        private decimal CalculateCashReturned(Check returnCheck)
        {
            if (!returnCheck.IsReturn) return 0m;

            Check originalCheck = null;
            if (returnCheck.OriginalCheckId.HasValue)
            {
                // Используем синхронный вызов, т.к. вызывается из синхронного контекста Sum()
                // В идеале, данные оригинальных чеков нужно было бы загрузить заранее асинхронно.
                try
                {
                    originalCheck = _checkRepository.GetCheckById(returnCheck.OriginalCheckId.Value);
                }
                catch
                {
                    // Ошибка загрузки оригинала - считаем возврат наличными
                    return returnCheck.TotalAmount;
                }
            }

            string originalPaymentTypeLower = originalCheck?.PaymentType?.ToLower();
            decimal returnTotal = returnCheck.TotalAmount;

            if (originalPaymentTypeLower == "cash") { return returnTotal; }
            else if (originalPaymentTypeLower == "card") { return 0m; }
            else if (originalPaymentTypeLower == "mixed")
            {
                decimal originalCardPaid = originalCheck?.CardPaid ?? 0m;
                if (returnTotal <= originalCardPaid) { return 0m; }
                else { return returnTotal - originalCardPaid; }
            }
            else { return returnTotal; }
        }
        public async Task<decimal> CalculateCurrentCashInDrawerAsync(Shift shift)
        {
            if (shift == null) return 0m;

            // Загружаем связанные данные
            var checks = await Task.Run(() => _checkRepository.GetChecksByShiftId(shift.ShiftId).ToList());
            var cashOps = await Task.Run(() => _cashDrawerRepository.GetOperationsByShiftId(shift.ShiftId).ToList());

            // Рассчитываем составляющие
            decimal cashSales = checks.Where(c => !c.IsReturn).Sum(c => c.PaymentType == "Cash" ? c.TotalAmount : c.PaymentType == "Mixed" ? c.CashPaid : 0);

            // Используем существующий приватный метод для расчета наличных, выданных при возвратах
            decimal cashReturns = checks.Where(c => c.IsReturn).Sum(c => CalculateCashReturned(c));

            decimal cashAdded = cashOps.Where(co => co.OperationType == "CashIn").Sum(co => co.Amount);
            decimal cashRemoved = cashOps.Where(co => co.OperationType == "CashOut").Sum(co => co.Amount);

            // Рассчитываем итоговую теоретическую сумму
            decimal currentCashTheoretic = shift.StartCash + cashSales + cashAdded - cashRemoved - cashReturns;

            return currentCashTheoretic;
        }
    }
}