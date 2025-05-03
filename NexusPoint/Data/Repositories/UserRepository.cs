using Dapper;
using NexusPoint.Models;
using NexusPoint.Utils;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;


namespace NexusPoint.Data.Repositories
{
    public class UserRepository
    {
        // Добавить нового пользователя (пароль будет хеширован)
        public int AddUser(User user, string plainPassword)
        {
            // Перед сохранением хешируем пароль
            user.HashedPassword = PasswordHasher.HashPassword(plainPassword); // Используем наш хешер

            using (var connection = DatabaseHelper.GetConnection())
            {
                string query = @"
                    INSERT INTO Users (Username, HashedPassword, FullName, Role)
                    VALUES (@Username, @HashedPassword, @FullName, @Role);
                    SELECT last_insert_rowid();";
                return connection.QuerySingle<int>(query, user);
            }
        }

        public User GetUserById(int userId)
        {
            using (var connection = DatabaseHelper.GetConnection())
            {
                return connection.QueryFirstOrDefault<User>("SELECT * FROM Users WHERE UserId = @Id", new { Id = userId });
            }
        }

        // Найти пользователя по логину (для входа в систему)
        public User GetUserByUsername(string username)
        {
            using (var connection = DatabaseHelper.GetConnection())
            {
                return connection.QueryFirstOrDefault<User>("SELECT * FROM Users WHERE Username = @Username", new { Username = username });
            }
        }

        // Получить всех пользователей
        public IEnumerable<User> GetAllUsers()
        {
            using (var connection = DatabaseHelper.GetConnection())
            {
                return connection.Query<User>("SELECT * FROM Users");
            }
        }

        // Обновить данные пользователя (кроме пароля)
        public bool UpdateUser(User user)
        {
            using (var connection = DatabaseHelper.GetConnection())
            {
                // Пароль обновляется отдельным методом для безопасности
                string query = @"
                    UPDATE Users SET
                        Username = @Username,
                        FullName = @FullName,
                        Role = @Role
                    WHERE UserId = @UserId";
                return connection.Execute(query, user) > 0;
            }
        }

        // Обновить пароль пользователя
        public bool UpdateUserPassword(int userId, string plainPassword)
        {
            string hashedPassword = PasswordHasher.HashPassword(plainPassword);
            using (var connection = DatabaseHelper.GetConnection())
            {
                string query = "UPDATE Users SET HashedPassword = @HashedPassword WHERE UserId = @UserId";
                return connection.Execute(query, new { HashedPassword = hashedPassword, UserId = userId }) > 0;
            }
        }

        // Удалить пользователя
        public bool DeleteUser(int userId)
        {
            using (var connection = DatabaseHelper.GetConnection())
            {
                // Подумать о связанных данных (чеки, смены). Может, не удалять, а деактивировать?
                string query = "DELETE FROM Users WHERE UserId = @Id";
                return connection.Execute(query, new { Id = userId }) > 0;
            }
        }
    }
}
