using System.Data.SQLite;
using System.IO;
using System.Reflection;

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
            connection.Open();
            using (var command = connection.CreateCommand())
            {
                command.CommandText = "PRAGMA foreign_keys = ON;";
                command.ExecuteNonQuery();
            }
            connection.Close();
            return connection;
        }

        public static void InitializeDatabaseIfNotExists()
        {
            string dbPath = GetDatabasePath();
            if (!File.Exists(dbPath))
            {
                SQLiteConnection.CreateFile(dbPath);
                DatabaseInitializer.CreateTables();
                System.Diagnostics.Debug.WriteLine($"Database file created at: {dbPath}");
            }
            else
            {
                System.Diagnostics.Debug.WriteLine($"Database file found at: {dbPath}");
            }
        }
    }
}