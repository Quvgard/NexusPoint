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
    public class ProductRepository
    {
        // Метод для создания StockItem (можно вынести в StockItemRepository)
        private void EnsureStockItemExists(int productId, SQLiteConnection connection, SQLiteTransaction transaction)
        {
            // Проверяем, есть ли уже запись остатка для этого товара
            var existingStock = connection.QueryFirstOrDefault<int?>(
                "SELECT StockItemId FROM StockItems WHERE ProductId = @ProductId",
                new { ProductId = productId }, transaction); // Важно выполнять в рамках транзакции

            if (existingStock == null)
            {
                // Создаем запись остатка с нулевым количеством
                connection.Execute(@"
                     INSERT INTO StockItems (ProductId, Quantity, LastUpdated)
                     VALUES (@ProductId, @Quantity, @LastUpdated);",
                    new { ProductId = productId, Quantity = 0m, LastUpdated = DateTime.Now }, // Используем 0m для decimal
                    transaction); // Выполняем в транзакции
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

        // Добавить новый товар (и создать для него запись остатка)
        public int AddProduct(Product product)
        {
            using (var connection = DatabaseHelper.GetConnection())
            {
                connection.Open(); // Откроем соединение явно для транзакции
                using (var transaction = connection.BeginTransaction()) // Начинаем транзакцию
                {
                    try
                    {
                        string insertProductQuery = @"
                            INSERT INTO Products (Barcode, ProductCode, Name, Price, IsMarked)
                            VALUES (@Barcode, @ProductCode, @Name, @Price, @IsMarked);
                            SELECT last_insert_rowid();";
                        int newProductId = connection.QuerySingle<int>(insertProductQuery, product, transaction); // Выполняем в транзакции

                        // Убеждаемся, что для нового товара есть запись остатка
                        EnsureStockItemExists(newProductId, connection, transaction);

                        transaction.Commit(); // Фиксируем изменения
                        return newProductId;
                    }
                    catch (Exception ex)
                    {
                        transaction.Rollback(); // Откатываем изменения в случае ошибки
                        System.Diagnostics.Debug.WriteLine($"Error adding product: {ex.Message}");
                        throw; // Пробрасываем исключение
                    }
                }
            }
        }

        // Получить товары по списку их ID
        public IEnumerable<Product> GetProductsByIds(IEnumerable<int> productIds)
        {
            // Если список ID пуст, возвращаем пустую коллекцию, чтобы избежать ошибки в SQL IN ()
            if (productIds == null || !productIds.Any())
            {
                return Enumerable.Empty<Product>();
            }

            using (var connection = DatabaseHelper.GetConnection())
            {
                // Используем параметр Dapper для передачи списка ID в оператор IN
                string query = "SELECT * FROM Products WHERE ProductId IN @Ids";
                return connection.Query<Product>(query, new { Ids = productIds });
            }
        }

        // Обновить товар (остатки не трогаем здесь, только каталог)
        public bool UpdateProduct(Product product)
        {
            using (var connection = DatabaseHelper.GetConnection())
            {
                string query = @"
                    UPDATE Products SET
                        Barcode = @Barcode,
                        ProductCode = @ProductCode,
                        Name = @Name,
                        Price = @Price,
                        IsMarked = @IsMarked
                    WHERE ProductId = @ProductId";
                return connection.Execute(query, product) > 0;
            }
        }

        // Удалить товар (каскадное удаление StockItem сработает благодаря FK)
        public bool DeleteProduct(int productId)
        {
            using (var connection = DatabaseHelper.GetConnection())
            {
                connection.Open(); // Откроем для транзакции
                using (var transaction = connection.BeginTransaction())
                {
                    try
                    {
                        // 1. СНАЧАЛА удаляем связанный остаток (если есть)
                        connection.Execute("DELETE FROM StockItems WHERE ProductId = @Id", new { Id = productId }, transaction);

                        // 2. ПОТОМ удаляем сам товар
                        int rowsAffected = connection.Execute("DELETE FROM Products WHERE ProductId = @Id", new { Id = productId }, transaction);

                        transaction.Commit(); // Фиксируем, если все успешно
                        return rowsAffected > 0;
                    }
                    catch (Exception ex)
                    {
                        transaction.Rollback(); // Откатываем при ошибке
                        System.Diagnostics.Debug.WriteLine($"Error deleting product {productId}: {ex.Message}");
                        throw; // Пробрасываем исключение
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