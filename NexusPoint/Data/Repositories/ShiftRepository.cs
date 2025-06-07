using Dapper;
using NexusPoint.Models;
using System;
using System.Collections.Generic;
using System.Linq;

namespace NexusPoint.Data.Repositories
{
    public class ShiftRepository
    {
        public Shift GetCurrentOpenShift()
        {
            using (var connection = DatabaseHelper.GetConnection())
            {
                string query = "SELECT * FROM Shifts WHERE IsClosed = 0 ORDER BY ShiftId DESC LIMIT 1";
                return connection.QueryFirstOrDefault<Shift>(query);
            }
        }
        public Shift OpenShift(int openingUserId, decimal startCash)
        {
            var openShift = GetCurrentOpenShift();
            if (openShift != null)
            {
                throw new InvalidOperationException($"Нельзя открыть новую смену, пока не закрыта смена №{openShift.ShiftNumber} (ID: {openShift.ShiftId})");
            }
            int lastShiftNumber = 0;
            using (var connection = DatabaseHelper.GetConnection())
            {
                lastShiftNumber = connection.QueryFirstOrDefault<int?>("SELECT MAX(ShiftNumber) FROM Shifts") ?? 0;
            }
            int newShiftNumber = lastShiftNumber + 1;
            var newShift = new Shift
            {
                ShiftNumber = newShiftNumber,
                OpenTimestamp = DateTime.Now,
                OpeningUserId = openingUserId,
                StartCash = startCash,
                IsClosed = false
            };

            using (var connection = DatabaseHelper.GetConnection())
            {
                string query = @"
                    INSERT INTO Shifts (ShiftNumber, OpenTimestamp, OpeningUserId, StartCash, IsClosed)
                    VALUES (@ShiftNumber, @OpenTimestamp, @OpeningUserId, @StartCash, @IsClosed);
                    SELECT last_insert_rowid();";
                int newShiftId = connection.QuerySingle<int>(query, newShift);
                newShift.ShiftId = newShiftId;
                return newShift;
            }
        }
        public bool CloseShift(int shiftId, int closingUserId, decimal endCashActual)
        {
            using (var connection = DatabaseHelper.GetConnection())
            {
                connection.Open();
                using (var transaction = connection.BeginTransaction())
                {
                    try
                    {
                        var shiftToClose = connection.QueryFirstOrDefault<Shift>(
                            "SELECT * FROM Shifts WHERE ShiftId = @Id AND IsClosed = 0",
                            new { Id = shiftId }, transaction);

                        if (shiftToClose == null)
                        {
                            throw new InvalidOperationException($"Смена с ID {shiftId} не найдена или уже закрыта.");
                        }
                        var checks = connection.Query<Check>(
                            "SELECT * FROM Checks WHERE ShiftId = @ShiftId",
                            new { ShiftId = shiftId }, transaction).ToList();

                        var cashOps = connection.Query<CashDrawerOperation>(
                            "SELECT * FROM CashDrawerOperations WHERE ShiftId = @ShiftId",
                            new { ShiftId = shiftId }, transaction).ToList();

                        decimal totalSales = checks.Where(c => !c.IsReturn).Sum(c => c.TotalAmount);
                        decimal totalReturns = checks.Where(c => c.IsReturn).Sum(c => c.TotalAmount);
                        decimal cashSales = checks.Where(c => !c.IsReturn).Sum(c => c.PaymentType == "Cash" ? c.TotalAmount : c.PaymentType == "Mixed" ? c.CashPaid : 0);
                        decimal cardSales = checks.Where(c => !c.IsReturn).Sum(c => c.PaymentType == "Card" ? c.TotalAmount : c.PaymentType == "Mixed" ? c.CardPaid : 0);
                        decimal cashReturns = checks.Where(c => c.IsReturn).Sum(c => c.TotalAmount);

                        decimal cashAdded = cashOps.Where(co => co.OperationType == "CashIn").Sum(co => co.Amount);
                        decimal cashRemoved = cashOps.Where(co => co.OperationType == "CashOut").Sum(co => co.Amount);
                        decimal endCashTheoretic = shiftToClose.StartCash + cashSales + cashAdded - cashRemoved - cashReturns;
                        decimal difference = endCashActual - endCashTheoretic;
                        string updateQuery = @"
                             UPDATE Shifts SET
                                 CloseTimestamp = @CloseTimestamp,
                                 ClosingUserId = @ClosingUserId,
                                 TotalSales = @TotalSales,
                                 TotalReturns = @TotalReturns,
                                 CashSales = @CashSales,
                                 CardSales = @CardSales,
                                 CashAdded = @CashAdded,
                                 CashRemoved = @CashRemoved,
                                 EndCashTheoretic = @EndCashTheoretic,
                                 EndCashActual = @EndCashActual,
                                 Difference = @Difference,
                                 IsClosed = 1
                             WHERE ShiftId = @ShiftId";

                        int rowsAffected = connection.Execute(updateQuery, new
                        {
                            CloseTimestamp = DateTime.Now,
                            ClosingUserId = closingUserId,
                            TotalSales = totalSales,
                            TotalReturns = totalReturns,
                            CashSales = cashSales,
                            CardSales = cardSales,
                            CashAdded = cashAdded,
                            CashRemoved = cashRemoved,
                            EndCashTheoretic = endCashTheoretic,
                            EndCashActual = endCashActual,
                            Difference = difference,
                            ShiftId = shiftId
                        }, transaction);

                        transaction.Commit();
                        return rowsAffected > 0;
                    }
                    catch (Exception ex)
                    {
                        transaction.Rollback();
                        System.Diagnostics.Debug.WriteLine($"Error closing shift: {ex.Message}");
                        throw;
                    }
                }
            }
        }

        public Shift GetShiftById(int shiftId)
        {
            using (var connection = DatabaseHelper.GetConnection())
            {
                return connection.QueryFirstOrDefault<Shift>("SELECT * FROM Shifts WHERE ShiftId = @Id", new { Id = shiftId });
            }
        }

        public IEnumerable<Shift> GetShiftsByDateRange(DateTime startDate, DateTime endDate)
        {
            using (var connection = DatabaseHelper.GetConnection())
            {
                string query = "SELECT * FROM Shifts WHERE OpenTimestamp >= @Start AND OpenTimestamp <= @End ORDER BY OpenTimestamp";
                return connection.Query<Shift>(query, new { Start = startDate, End = endDate });
            }
        }

        public IEnumerable<Shift> GetShiftsByIds(IEnumerable<int> shiftIds)
        {
            if (shiftIds == null || !shiftIds.Any()) return Enumerable.Empty<Shift>();
            using (var connection = DatabaseHelper.GetConnection())
            {
                return connection.Query<Shift>("SELECT * FROM Shifts WHERE ShiftId IN @Ids", new { Ids = shiftIds });
            }
        }
    }
}