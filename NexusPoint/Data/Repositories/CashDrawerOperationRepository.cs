using Dapper;
using NexusPoint.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NexusPoint.Data.Repositories
{
    public class CashDrawerOperationRepository
    {
        // Добавить операцию внесения/изъятия
        public int AddOperation(CashDrawerOperation operation)
        {
            // Устанавливаем время операции, если оно не задано
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

        // Получить все операции для конкретной смены
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