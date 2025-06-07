using Dapper;
using NexusPoint.Models;
using System;
using System.Collections.Generic;

namespace NexusPoint.Data.Repositories
{
    public class CashDrawerOperationRepository
    {
        public int AddOperation(CashDrawerOperation operation)
        {
            if (operation.Timestamp == default(DateTime))
            {
                operation.Timestamp = DateTime.Now;
            }

            using (var connection = DatabaseHelper.GetConnection())
            {
                string query = @"
                    INSERT INTO CashDrawerOperations (ShiftId, UserId, Timestamp, OperationType, Amount, Reason)
                    VALUES (@ShiftId, @UserId, @Timestamp, @OperationType, @Amount, @Reason);
                    SELECT last_insert_rowid();";
                return connection.QuerySingle<int>(query, operation);
            }
        }
        public IEnumerable<CashDrawerOperation> GetOperationsByShiftId(int shiftId)
        {
            using (var connection = DatabaseHelper.GetConnection())
            {
                string query = "SELECT * FROM CashDrawerOperations WHERE ShiftId = @ShiftId ORDER BY Timestamp";
                return connection.Query<CashDrawerOperation>(query, new { ShiftId = shiftId });
            }
        }
    }
}