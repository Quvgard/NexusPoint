using Dapper;
using NexusPoint.Models;
using System;
using System.Collections.Generic;
using System.Data.SQLite;
using System.Linq;

namespace NexusPoint.Data.Repositories
{
    public class CheckRepository
    {
        private readonly StockItemRepository _stockItemRepository = new StockItemRepository();
        public int GetNextCheckNumber(int shiftId)
        {
            using (var connection = DatabaseHelper.GetConnection())
            {
                int? lastCheckNumber = connection.QueryFirstOrDefault<int?>(
                    "SELECT MAX(CheckNumber) FROM Checks WHERE ShiftId = @ShiftId",
                    new { ShiftId = shiftId });
                return (lastCheckNumber ?? 0) + 1;
            }
        }
        public Check AddCheck(Check check)
        {
            if (check.Items == null || !check.Items.Any())
            {
                throw new ArgumentException("Check must contain at least one item.");
            }
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
                        string insertCheckQuery = @"
                            INSERT INTO Checks (ShiftId, CheckNumber, Timestamp, UserId, TotalAmount, PaymentType, CashPaid, CardPaid, DiscountAmount, IsReturn, OriginalCheckId)
                            VALUES (@ShiftId, @CheckNumber, @Timestamp, @UserId, @TotalAmount, @PaymentType, @CashPaid, @CardPaid, @DiscountAmount, @IsReturn, @OriginalCheckId);
                            SELECT last_insert_rowid();";
                        int newCheckId = connection.QuerySingle<int>(insertCheckQuery, check, transaction);
                        check.CheckId = newCheckId;
                        string insertItemQuery = @"
                           INSERT INTO CheckItems (CheckId, ProductId, Quantity, PriceAtSale, ItemTotalAmount, DiscountAmount, AppliedDiscountId)
                            VALUES (@CheckId, @ProductId, @Quantity, @PriceAtSale, @ItemTotalAmount, @DiscountAmount, @AppliedDiscountId);";

                        foreach (var item in check.Items)
                        {
                            item.ItemTotalAmount = Math.Round(item.Quantity * item.PriceAtSale - item.DiscountAmount, 2);
                            item.CheckId = newCheckId;
                            connection.Execute(insertItemQuery, item, transaction);
                            decimal quantityChange = check.IsReturn ? item.Quantity : -item.Quantity;

                            _stockItemRepository.EnsureStockItemExists(item.ProductId, connection, transaction);

                            bool stockUpdated = _stockItemRepository.UpdateStockQuantity(item.ProductId, quantityChange, connection, transaction);

                            if (!stockUpdated && check.IsReturn)
                            {
                                throw new InvalidOperationException($"Не удалось обновить остаток при возврате для товара ID {item.ProductId} после EnsureStockItemExists.");
                            }
                        }

                        transaction.Commit();
                        return check;
                    }
                    catch (Exception ex)
                    {
                        transaction.Rollback();
                        System.Diagnostics.Debug.WriteLine($"Error adding check: {ex.Message}");
                        throw;
                    }
                }
            }
        }
        public Check GetCheckById(int checkId)
        {
            using (var connection = DatabaseHelper.GetConnection())
            {
                var check = connection.QueryFirstOrDefault<Check>("SELECT * FROM Checks WHERE CheckId = @Id", new { Id = checkId });
                if (check != null)
                {
                    check.Items = GetCheckItemsByCheckId(checkId, connection);
                }
                return check;
            }
        }
        public List<CheckItem> GetCheckItemsByCheckId(int checkId, SQLiteConnection connection = null)
        {
            bool closeConnection = false;
            if (connection == null)
            {
                connection = DatabaseHelper.GetConnection();
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
                }
            }
        }
        public IEnumerable<Check> GetChecksByShiftId(int shiftId)
        {
            using (var connection = DatabaseHelper.GetConnection())
            {
                string query = "SELECT * FROM Checks WHERE ShiftId = @ShiftId ORDER BY Timestamp";
                return connection.Query<Check>(query, new { ShiftId = shiftId });
            }
        }
        public Check FindCheckByNumberAndShift(int checkNumber, int shiftNumber)
        {
            using (var connection = DatabaseHelper.GetConnection())
            {
                var shift = connection.QueryFirstOrDefault<Shift>(
                    "SELECT * FROM Shifts WHERE ShiftNumber = @ShiftNumber ORDER BY ShiftId DESC LIMIT 1",
                    new { ShiftNumber = shiftNumber });

                if (shift == null) return null;
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
                var check = connection.QueryFirstOrDefault<Check>("SELECT * FROM Checks ORDER BY CheckId DESC LIMIT 1");
                if (check != null)
                {
                    check.Items = GetCheckItemsByCheckId(check.CheckId, connection);
                }
                return check;
            }
        }
    }
}