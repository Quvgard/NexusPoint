using Dapper;
using NexusPoint.Models;
using NexusPoint.Utils;
using System.Collections.Generic;
using System.Linq;


namespace NexusPoint.Data.Repositories
{
    public class UserRepository
    {
        public int AddUser(User user, string plainPassword)
        {
            user.HashedPassword = PasswordHasher.HashPassword(plainPassword);

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
        public User GetUserByUsername(string username)
        {
            using (var connection = DatabaseHelper.GetConnection())
            {
                return connection.QueryFirstOrDefault<User>("SELECT * FROM Users WHERE Username = @Username", new { Username = username });
            }
        }
        public IEnumerable<User> GetAllUsers()
        {
            using (var connection = DatabaseHelper.GetConnection())
            {
                return connection.Query<User>("SELECT * FROM Users");
            }
        }
        public bool UpdateUser(User user)
        {
            using (var connection = DatabaseHelper.GetConnection())
            {
                string query = @"
                    UPDATE Users SET
                        Username = @Username,
                        FullName = @FullName,
                        Role = @Role
                    WHERE UserId = @UserId";
                return connection.Execute(query, user) > 0;
            }
        }
        public bool UpdateUserPassword(int userId, string plainPassword)
        {
            string hashedPassword = PasswordHasher.HashPassword(plainPassword);
            using (var connection = DatabaseHelper.GetConnection())
            {
                string query = "UPDATE Users SET HashedPassword = @HashedPassword WHERE UserId = @UserId";
                return connection.Execute(query, new { HashedPassword = hashedPassword, UserId = userId }) > 0;
            }
        }
        public bool DeleteUser(int userId)
        {
            using (var connection = DatabaseHelper.GetConnection())
            {
                string query = "DELETE FROM Users WHERE UserId = @Id";
                return connection.Execute(query, new { Id = userId }) > 0;
            }
        }

        public IEnumerable<User> GetUsersByIds(IEnumerable<int> userIds)
        {
            if (userIds == null || !userIds.Any()) return Enumerable.Empty<User>();
            using (var connection = DatabaseHelper.GetConnection())
            {
                return connection.Query<User>("SELECT * FROM Users WHERE UserId IN @Ids", new { Ids = userIds });
            }
        }
    }
}
