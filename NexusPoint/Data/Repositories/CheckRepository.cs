using Dapper;
using NexusPoint.Models;
using System;
using System.Collections.Generic;
using System.Data.SQLite;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NexusPoint.Data.Repositories
{
    public class CheckRepository
    {
        // Зависимость от репозитория остатков (простой вариант без DI)
        private readonly StockItemRepository _stockItemRepository = new StockItemRepository();

        // Получить следующий номер чека для текущей открытой смены
        public int GetNextCheckNumber(int shiftId)
        {
            using (var connection = DatabaseHelper.GetConnection())
            {
                // Ищем максимальный номер чека в этой смене
                int? lastCheckNumber = connection.QueryFirstOrDefault<int?>(
                    "SELECT MAX(CheckNumber) FROM Checks WHERE ShiftId = @ShiftId",
                    new { ShiftId = shiftId });
                return (lastCheckNumber ?? 0) + 1; // Если чеков не было, начинаем с 1
            }
        }

        // Добавить новый чек (продажа или возврат)
        // ВАЖНО: Объект check должен приходить с заполненным списком Items!
        public Check AddCheck(Check check)
        {
            if (check.Items == null || !check.Items.Any())
            {
                throw new ArgumentException("Check must contain at least one item.");
            }

            // Устанавливаем время, если не задано
            if (check.Timestamp == default(DateTime))
            {
                check.Timestamp = DateTime.Now;
            }

            using (var connection = DatabaseHelper.GetConnection())
            {
                connection.Open();
                using (var transaction = connection.BeginTransaction())
                {
                    try
                    {
                        // 1. Вставить основную запись чека
                        string insertCheckQuery = @"
                            INSERT INTO Checks (ShiftId, CheckNumber, Timestamp, UserId, TotalAmount, PaymentType, CashPaid, CardPaid, DiscountAmount, IsReturn, OriginalCheckId)
                            VALUES (@ShiftId, @CheckNumber, @Timestamp, @UserId, @TotalAmount, @PaymentType, @CashPaid, @CardPaid, @DiscountAmount, @IsReturn, @OriginalCheckId);
                            SELECT last_insert_rowid();";
                        int newCheckId = connection.QuerySingle<int>(insertCheckQuery, check, transaction);
                        check.CheckId = newCheckId; // Присвоить ID объекту

                        // 2. Вставить позиции чека
                        string insertItemQuery = @"
                           INSERT INTO CheckItems (CheckId, ProductId, Quantity, PriceAtSale, ItemTotalAmount, DiscountAmount, AppliedDiscountId)
                            VALUES (@CheckId, @ProductId, @Quantity, @PriceAtSale, @ItemTotalAmount, @DiscountAmount, @AppliedDiscountId);";

                        foreach (var item in check.Items)
                        {
                            item.ItemTotalAmount = Math.Round(item.Quantity * item.PriceAtSale - item.DiscountAmount, 2);
                            item.CheckId = newCheckId;
                            connection.Execute(insertItemQuery, item, transaction);

                            // 3. Обновить остатки товаров
                            decimal quantityChange = check.IsReturn ? item.Quantity : -item.Quantity;

                            _stockItemRepository.EnsureStockItemExists(item.ProductId, connection, transaction);

                            bool stockUpdated = _stockItemRepository.UpdateStockQuantity(item.ProductId, quantityChange, connection, transaction);

                            if (!stockUpdated && check.IsReturn)
                            {
                                // Если при ВОЗВРАТЕ не удалось обновить остаток даже после Ensure, это проблема
                                throw new InvalidOperationException($"Не удалось обновить остаток при возврате для товара ID {item.ProductId} после EnsureStockItemExists.");
                            }
                        }

                        transaction.Commit(); // Все успешно, фиксируем
                        return check;
                    }
                    catch (Exception ex)
                    {
                        transaction.Rollback(); // Откат при любой ошибке
                        System.Diagnostics.Debug.WriteLine($"Error adding check: {ex.Message}");
                        throw;
                    }
                }
            }
        }

        // Получить чек по ID (с загрузкой позиций)
        public Check GetCheckById(int checkId)
        {
            using (var connection = DatabaseHelper.GetConnection())
            {
                var check = connection.QueryFirstOrDefault<Check>("SELECT * FROM Checks WHERE CheckId = @Id", new { Id = checkId });
                if (check != null)
                {
                    // Загружаем связанные позиции
                    check.Items = GetCheckItemsByCheckId(checkId, connection);
                }
                return check;
            }
        }

        // Получить позиции для чека (вспомогательный метод)
        public List<CheckItem> GetCheckItemsByCheckId(int checkId, SQLiteConnection connection = null)
        {
            bool closeConnection = false;
            if (connection == null)
            {
                connection = DatabaseHelper.GetConnection();
                // connection.Open(); // Dapper откроет сам
                closeConnection = true;
            }
            try
            {
                string query = "SELECT * FROM CheckItems WHERE CheckId = @CheckId";
                return connection.Query<CheckItem>(query, new { CheckId = checkId }).ToList();
            }
            finally
            {
                if (closeConnection)
                {
                    // connection.Close(); // Dapper закроет сам
                }
            }
        }

        // Получить чеки для смены
        public IEnumerable<Check> GetChecksByShiftId(int shiftId)
        {
            using (var connection = DatabaseHelper.GetConnection())
            {
                // Здесь можно решить, загружать ли позиции сразу
                // Пока получаем только основные данные чеков
                string query = "SELECT * FROM Checks WHERE ShiftId = @ShiftId ORDER BY Timestamp";
                return connection.Query<Check>(query, new { ShiftId = shiftId });
                // Если нужно загрузить с позициями:
                // var checks = connection.Query<Check>(query, new { ShiftId = shiftId }).ToList();
                // foreach (var check in checks) { check.Items = GetCheckItemsByCheckId(check.CheckId, connection); }
                // return checks;
            }
        }

        // Найти чек по номеру чека и номеру смены
        public Check FindCheckByNumberAndShift(int checkNumber, int shiftNumber) // Или по shiftId?
        {
            using (var connection = DatabaseHelper.GetConnection())
            {
                // Ищем сначала смену по номеру
                var shift = connection.QueryFirstOrDefault<Shift>(
                    "SELECT * FROM Shifts WHERE ShiftNumber = @ShiftNumber ORDER BY ShiftId DESC LIMIT 1", // Берем последнюю с таким номером
                    new { ShiftNumber = shiftNumber });

                if (shift == null) return null; // Смена не найдена

                // Ищем чек в найденной смене
                var check = connection.QueryFirstOrDefault<Check>(
                    "SELECT * FROM Checks WHERE CheckNumber = @CheckNumber AND ShiftId = @ShiftId",
                    new { CheckNumber = checkNumber, ShiftId = shift.ShiftId });

                if (check != null)
                {
                    check.Items = GetCheckItemsByCheckId(check.CheckId, connection);
                }
                return check;
            }
        }

        public Check GetLastCheck()
        {
            using (var connection = DatabaseHelper.GetConnection())
            {
                // Получаем самый последний чек по ID
                var check = connection.QueryFirstOrDefault<Check>("SELECT * FROM Checks ORDER BY CheckId DESC LIMIT 1");
                if (check != null)
                {
                    // Дозагружаем позиции
                    check.Items = GetCheckItemsByCheckId(check.CheckId, connection);
                }
                return check;
            }
        }
    }
}