using Dapper;
using NexusPoint.Models;
using System;
using System.Collections.Generic;
using System.Data.SQLite;

namespace NexusPoint.Data.Repositories
{
    public class StockItemRepository
    {
        public decimal GetStockQuantity(int productId)
        {
            using (var connection = DatabaseHelper.GetConnection())
            {
                string query = "SELECT Quantity FROM StockItems WHERE ProductId = @ProductId";
                decimal? quantity = connection.QueryFirstOrDefault<decimal?>(query, new { ProductId = productId });
                return quantity ?? 0m;
            }
        }
        public StockItem GetStockItem(int productId)
        {
            using (var connection = DatabaseHelper.GetConnection())
            {
                string query = "SELECT * FROM StockItems WHERE ProductId = @ProductId";
                return connection.QueryFirstOrDefault<StockItem>(query, new { ProductId = productId });
            }
        }
        public bool UpdateStockQuantity(int productId, decimal quantityChange, SQLiteConnection connection = null, SQLiteTransaction transaction = null)
        {
            bool closeConnection = false;
            if (connection == null)
            {
                connection = DatabaseHelper.GetConnection();
                connection.Open();
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
                }, transaction);
                if (rowsAffected == 0)
                {
                    System.Diagnostics.Debug.WriteLine($"Warning: Stock update failed for ProductId {productId}. Item might not exist in StockItems.");
                }

                return rowsAffected > 0;
            }
            finally
            {
                if (closeConnection)
                {
                    connection.Close();
                }
            }
        }
        public IEnumerable<StockItem> GetAllStockItems()
        {
            using (var connection = DatabaseHelper.GetConnection())
            {
                string query = "SELECT * FROM StockItems";
                return connection.Query<StockItem>(query);
            }
        }
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
        public void EnsureStockItemExists(int productId, SQLiteConnection connection = null, SQLiteTransaction transaction = null)
        {
            SQLiteConnection conn = connection;
            bool closeConnection = false;
            if (conn == null)
            {
                conn = DatabaseHelper.GetConnection();
                closeConnection = true;
                transaction = null;
            }

            try
            {
                var existingStock = conn.QueryFirstOrDefault<int?>(
                    "SELECT StockItemId FROM StockItems WHERE ProductId = @ProductId",
                    new { ProductId = productId }, transaction);

                if (existingStock == null)
                {
                    conn.Execute(@"
                 INSERT INTO StockItems (ProductId, Quantity, LastUpdated)
                 VALUES (@ProductId, @Quantity, @LastUpdated);",
                        new { ProductId = productId, Quantity = 0m, LastUpdated = DateTime.Now },
                        transaction);
                    System.Diagnostics.Debug.WriteLine($"StockItem created for ProductId {productId}.");
                }
            }
            finally
            {
                if (closeConnection && conn != null)
                {
                }
            }
        }
    }
}