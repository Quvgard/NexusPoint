using System;
using System.Collections.Generic;
using System.Data.Entity;
using System.Data.SQLite;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using System.IO;

namespace NexusPoint.Data
{
    public static class DatabaseHelper
    {
        private static readonly string dbFileName = "NexusPoint.sqlite";
        private static readonly string connectionString = $"Data Source={GetDatabasePath()};Version=3;";

        private static string GetDatabasePath()
        {
            string exePath = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
            return Path.Combine(exePath, dbFileName);
        }

        public static SQLiteConnection GetConnection()
        {
            var connection = new SQLiteConnection(connectionString);
            // Включение поддержки внешних ключей для этого соединения
            // Это важно для работы ON DELETE CASCADE и других ограничений FK
            connection.Open(); // Откроем здесь, чтобы выполнить PRAGMA
            using (var command = connection.CreateCommand())
            {
                command.CommandText = "PRAGMA foreign_keys = ON;";
                command.ExecuteNonQuery();
            }
            connection.Close(); // Закроем обратно, Dapper сам откроет когда нужно
            return connection;
        }

        public static void InitializeDatabaseIfNotExists()
        {
            string dbPath = GetDatabasePath();
            if (!File.Exists(dbPath))
            {
                SQLiteConnection.CreateFile(dbPath);
                DatabaseInitializer.CreateTables(); // Вызываем обновленный метод
                System.Diagnostics.Debug.WriteLine($"Database file created at: {dbPath}");
            }
            else
            {
                System.Diagnostics.Debug.WriteLine($"Database file found at: {dbPath}");
                // В реальном приложении здесь могла бы быть логика миграции схемы,
                // но для начала просто проверяем/создаем.
                // DatabaseInitializer.CreateTables(); // Можно вызывать всегда для IF NOT EXISTS
            }
        }
    }
}