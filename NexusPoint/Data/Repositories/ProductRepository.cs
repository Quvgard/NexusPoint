using Dapper;
using NexusPoint.Models;
using System;
using System.Collections.Generic;
using System.Data.SQLite;
using System.Linq;

namespace NexusPoint.Data.Repositories
{
    public class ProductRepository
    {
        private void EnsureStockItemExists(int productId, SQLiteConnection connection, SQLiteTransaction transaction)
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
            }
        }

        public IEnumerable<Product> GetAllProducts()
        {
            using (var connection = DatabaseHelper.GetConnection())
            {
                return connection.Query<Product>("SELECT * FROM Products");
            }
        }

        public Product FindProductById(int productId)
        {
            using (var connection = DatabaseHelper.GetConnection())
            {
                return connection.QueryFirstOrDefault<Product>("SELECT * FROM Products WHERE ProductId = @Id", new { Id = productId });
            }
        }


        public Product FindProductByCodeOrBarcode(string codeOrBarcode)
        {
            using (var connection = DatabaseHelper.GetConnection())
            {
                string query = @"
                    SELECT * FROM Products
                    WHERE Barcode = @Identifier OR ProductCode = @Identifier
                    LIMIT 1";
                return connection.QueryFirstOrDefault<Product>(query, new { Identifier = codeOrBarcode });
            }
        }
        public int AddProduct(Product product)
        {
            using (var connection = DatabaseHelper.GetConnection())
            {
                connection.Open();
                using (var transaction = connection.BeginTransaction())
                {
                    try
                    {
                        string insertProductQuery = @"
                            INSERT INTO Products (Barcode, ProductCode, Name, Description, Price)
                            VALUES (@Barcode, @ProductCode, @Name, @Description, @Price);
                            SELECT last_insert_rowid();";
                        int newProductId = connection.QuerySingle<int>(insertProductQuery, product, transaction);
                        var stockRepo = new StockItemRepository();
                        stockRepo.EnsureStockItemExists(newProductId, connection, transaction);

                        transaction.Commit();
                        return newProductId;
                    }
                    catch (Exception ex)
                    {
                        transaction.Rollback();
                        System.Diagnostics.Debug.WriteLine($"Error adding product: {ex.Message}");
                        throw;
                    }
                }
            }
        }
        public IEnumerable<Product> GetProductsByIds(IEnumerable<int> productIds)
        {
            if (productIds == null || !productIds.Any())
            {
                return Enumerable.Empty<Product>();
            }

            using (var connection = DatabaseHelper.GetConnection())
            {
                string query = "SELECT * FROM Products WHERE ProductId IN @Ids";
                return connection.Query<Product>(query, new { Ids = productIds });
            }
        }
        public bool UpdateProduct(Product product)
        {
            using (var connection = DatabaseHelper.GetConnection())
            {
                string query = @"
                    UPDATE Products SET
                        Barcode = @Barcode,
                        ProductCode = @ProductCode,
                        Name = @Name,
                        Description = @Description, 
                        Price = @Price
                    WHERE ProductId = @ProductId";
                return connection.Execute(query, product) > 0;
            }
        }
        public bool DeleteProduct(int productId)
        {
            using (var connection = DatabaseHelper.GetConnection())
            {
                connection.Open();
                using (var transaction = connection.BeginTransaction())
                {
                    try
                    {
                        connection.Execute("DELETE FROM StockItems WHERE ProductId = @Id", new { Id = productId }, transaction);
                        int rowsAffected = connection.Execute("DELETE FROM Products WHERE ProductId = @Id", new { Id = productId }, transaction);

                        transaction.Commit();
                        return rowsAffected > 0;
                    }
                    catch (Exception ex)
                    {
                        transaction.Rollback();
                        System.Diagnostics.Debug.WriteLine($"Error deleting product {productId}: {ex.Message}");
                        throw;
                    }
                }
            }
        }

        public IEnumerable<Product> SearchProductsByName(string searchTerm)
        {
            using (var connection = DatabaseHelper.GetConnection())
            {
                string query = "SELECT * FROM Products WHERE Name LIKE @SearchPattern";
                return connection.Query<Product>(query, new { SearchPattern = $"%{searchTerm}%" });
            }
        }
    }
}