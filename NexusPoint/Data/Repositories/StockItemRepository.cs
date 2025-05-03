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
    public class StockItemRepository
    {
        // Получить текущий остаток товара
        public decimal GetStockQuantity(int productId)
        {
            using (var connection = DatabaseHelper.GetConnection())
            {
                string query = "SELECT Quantity FROM StockItems WHERE ProductId = @ProductId";
                // QueryFirstOrDefault<decimal?> вернет null, если товара нет в остатках
                decimal? quantity = connection.QueryFirstOrDefault<decimal?>(query, new { ProductId = productId });
                return quantity ?? 0m; // Возвращаем 0, если записи нет
            }
        }

        // Получить полный объект StockItem
        public StockItem GetStockItem(int productId)
        {
            using (var connection = DatabaseHelper.GetConnection())
            {
                string query = "SELECT * FROM StockItems WHERE ProductId = @ProductId";
                return connection.QueryFirstOrDefault<StockItem>(query, new { ProductId = productId });
            }
        }


        // Обновить остаток (добавить или списать)
        // Возвращает true, если обновление успешно
        public bool UpdateStockQuantity(int productId, decimal quantityChange, SQLiteConnection connection = null, SQLiteTransaction transaction = null)
        {
            // Этот метод может вызываться как самостоятельно, так и в рамках внешней транзакции (например, при сохранении чека)
            bool closeConnection = false;
            if (connection == null)
            {
                connection = DatabaseHelper.GetConnection();
                connection.Open(); // Открываем, если не передали открытое
                closeConnection = true;
            }

            try
            {
                string query = @"
                     UPDATE StockItems
                     SET Quantity = Quantity + @Change, -- Добавляем или вычитаем
                         LastUpdated = @Now
                     WHERE ProductId = @ProductId";

                int rowsAffected = connection.Execute(query, new
                {
                    Change = quantityChange,
                    Now = DateTime.Now,
                    ProductId = productId
                }, transaction); // Используем переданную транзакцию, если она есть

                // Дополнительная проверка: если товара не было в StockItems, обновление не произойдет (rowsAffected = 0)
                // В этом случае, если мы списываем (quantityChange < 0), это ошибка.
                // Если добавляем (quantityChange > 0) - возможно, это приемка и нужно создать запись?
                // Пока считаем, что запись StockItem должна существовать перед обновлением.
                if (rowsAffected == 0)
                {
                    System.Diagnostics.Debug.WriteLine($"Warning: Stock update failed for ProductId {productId}. Item might not exist in StockItems.");
                    // Можно добавить логику создания записи здесь, если это приемлемо
                    // EnsureStockItemExists(productId, connection, transaction); // Потребует вынести EnsureStockItemExists в этот репозиторий или сделать его public static
                }

                return rowsAffected > 0;
            }
            finally
            {
                if (closeConnection)
                {
                    connection.Close(); // Закрываем соединение, только если мы его сами открыли
                }
            }
        }

        // Получить все записи остатков
        public IEnumerable<StockItem> GetAllStockItems()
        {
            using (var connection = DatabaseHelper.GetConnection())
            {
                // Просто выбираем все записи из таблицы остатков
                string query = "SELECT * FROM StockItems";
                return connection.Query<StockItem>(query);
            }
        }

        // Установить точное значение остатка (для инвентаризации)
        public bool SetStockQuantity(int productId, decimal newQuantity)
        {
            using (var connection = DatabaseHelper.GetConnection())
            {
                string query = @"
                     UPDATE StockItems
                     SET Quantity = @NewQuantity,
                         LastUpdated = @Now
                     WHERE ProductId = @ProductId";
                return connection.Execute(query, new { NewQuantity = newQuantity, Now = DateTime.Now, ProductId = productId }) > 0;
            }
        }

        // Метод создания записи, если его нет (вызывается из ProductRepository)
        // Сделаем его доступным для ProductRepository
        public void EnsureStockItemExists(int productId, SQLiteConnection connection, SQLiteTransaction transaction)
        {
            var existingStock = connection.QueryFirstOrDefault<int?>(
                "SELECT StockItemId FROM StockItems WHERE ProductId = @ProductId",
                new { ProductId = productId }, transaction);

            if (existingStock == null)
            {
                connection.Execute(@"
                     INSERT INTO StockItems (ProductId, Quantity, LastUpdated)
                     VALUES (@ProductId, @Quantity, @LastUpdated);",
                    new { ProductId = productId, Quantity = 0m, LastUpdated = DateTime.Now },
                    transaction);
                System.Diagnostics.Debug.WriteLine($"StockItem created for ProductId {productId}.");
            }
        }
    }
}