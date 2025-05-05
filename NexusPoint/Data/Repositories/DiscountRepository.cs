using Dapper;
using NexusPoint.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NexusPoint.Data.Repositories
{
    public class DiscountRepository
    {
        public int AddDiscount(Discount discount)
        {
            using (var connection = DatabaseHelper.GetConnection())
            {
                string query = @"
                    INSERT INTO Discounts (Name, Type, Value, RequiredProductId, GiftProductId, StartDate, EndDate, IsActive)
                    VALUES (@Name, @Type, @Value, @RequiredProductId, @GiftProductId, @StartDate, @EndDate, @IsActive);
                    SELECT last_insert_rowid();";
                return connection.QuerySingle<int>(query, discount);
            }
        }

        public IEnumerable<Discount> GetDiscountsByIds(IEnumerable<int> discountIds)
        {
            // Проверка на пустой список ID
            if (discountIds == null || !discountIds.Any())
            {
                return Enumerable.Empty<Discount>(); // Возвращаем пустую коллекцию
            }

            using (var connection = DatabaseHelper.GetConnection())
            {
                // Используем оператор IN для выборки по списку ID
                string query = "SELECT * FROM Discounts WHERE DiscountId IN @Ids";
                return connection.Query<Discount>(query, new { Ids = discountIds });
            }

        }
        public Discount GetDiscountById(int discountId)
        {
            using (var connection = DatabaseHelper.GetConnection())
            {
                return connection.QueryFirstOrDefault<Discount>("SELECT * FROM Discounts WHERE DiscountId = @Id", new { Id = discountId });
            }
        }

        // Получить все активные на текущий момент скидки
        public IEnumerable<Discount> GetAllActiveDiscounts()
        {
            using (var connection = DatabaseHelper.GetConnection())
            {
                DateTime now = DateTime.Now;
                // Выбираем активные, у которых дата старта прошла (или null), и дата окончания не наступила (или null)
                string query = @"
                    SELECT * FROM Discounts
                    WHERE IsActive = 1
                      AND (StartDate IS NULL OR StartDate <= @Now)
                      AND (EndDate IS NULL OR EndDate >= @Now)";
                return connection.Query<Discount>(query, new { Now = now });
            }
        }

        public IEnumerable<Discount> GetAllDiscounts()
        {
            using (var connection = DatabaseHelper.GetConnection())
            {
                return connection.Query<Discount>("SELECT * FROM Discounts");
            }
        }


        public bool UpdateDiscount(Discount discount)
        {
            using (var connection = DatabaseHelper.GetConnection())
            {
                string query = @"
                    UPDATE Discounts SET
                        Name = @Name, Type = @Type, Value = @Value, RequiredProductId = @RequiredProductId,
                        GiftProductId = @GiftProductId, StartDate = @StartDate, EndDate = @EndDate, IsActive = @IsActive
                    WHERE DiscountId = @DiscountId";
                return connection.Execute(query, discount) > 0;
            }
        }

        public bool DeleteDiscount(int discountId)
        {
            using (var connection = DatabaseHelper.GetConnection())
            {
                string query = "DELETE FROM Discounts WHERE DiscountId = @Id";
                return connection.Execute(query, new { Id = discountId }) > 0;
            }
        }
    }
}