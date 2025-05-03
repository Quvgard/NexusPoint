using Dapper;
using System;
using System.Collections.Generic;
using System.Data.SQLite;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;

namespace NexusPoint.Data
{
    public static class DatabaseInitializer
    {
        public static void CreateTables()
        {
            // Используем using для гарантии закрытия соединения
            using (var connection = DatabaseHelper.GetConnection())
            {
                try
                {
                    // Открывать соединение не нужно, Dapper сделает это сам.
                    // DatabaseHelper уже открывал для PRAGMA.

                    // --- Таблица Пользователей ---
                    connection.Execute(@"
                CREATE TABLE IF NOT EXISTS Users (
                    UserId INTEGER PRIMARY KEY AUTOINCREMENT,
                    Username TEXT NOT NULL UNIQUE,
                    HashedPassword TEXT NOT NULL,
                    FullName TEXT NOT NULL,
                    Role TEXT NOT NULL CHECK(Role IN ('Cashier', 'Manager', 'Admin'))
                );");
                    System.Diagnostics.Debug.WriteLine("Users table checked/created.");

                    // --- Таблица Товаров (Каталог) ---
                    connection.Execute(@"
                CREATE TABLE IF NOT EXISTS Products (
                    ProductId INTEGER PRIMARY KEY AUTOINCREMENT,
                    Barcode TEXT UNIQUE,
                    ProductCode TEXT NOT NULL UNIQUE,
                    Name TEXT NOT NULL,
                    Price REAL NOT NULL,
                    IsMarked INTEGER NOT NULL DEFAULT 0
                );");
                    System.Diagnostics.Debug.WriteLine("Products (Catalog) table checked/created.");

                    // --- Таблица Остатков Товаров ---
                    connection.Execute(@"
                CREATE TABLE IF NOT EXISTS StockItems (
                    StockItemId INTEGER PRIMARY KEY AUTOINCREMENT,
                    ProductId INTEGER NOT NULL UNIQUE,      -- Один товар - одна запись остатка
                    Quantity REAL NOT NULL DEFAULT 0,       -- Используем REAL для гибкости
                    LastUpdated DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
                    FOREIGN KEY (ProductId) REFERENCES Products(ProductId) ON DELETE CASCADE
                );");
                    System.Diagnostics.Debug.WriteLine("StockItems table checked/created.");

                    // --- Таблица Смен ---
                    connection.Execute(@"
                CREATE TABLE IF NOT EXISTS Shifts (
                    ShiftId INTEGER PRIMARY KEY AUTOINCREMENT,
                    ShiftNumber INTEGER NOT NULL,           -- Подумать над уникальностью (напр., в рамках дня/кассы)
                    OpenTimestamp DATETIME NOT NULL,
                    CloseTimestamp DATETIME NULL,
                    OpeningUserId INTEGER NOT NULL,
                    ClosingUserId INTEGER NULL,
                    StartCash REAL NOT NULL DEFAULT 0,
                    TotalSales REAL NULL,
                    TotalReturns REAL NULL,
                    CashSales REAL NULL,
                    CardSales REAL NULL,
                    CashAdded REAL NULL,
                    CashRemoved REAL NULL,
                    EndCashTheoretic REAL NULL,
                    EndCashActual REAL NULL,
                    Difference REAL NULL,
                    IsClosed INTEGER NOT NULL DEFAULT 0,
                    FOREIGN KEY (OpeningUserId) REFERENCES Users(UserId),
                    FOREIGN KEY (ClosingUserId) REFERENCES Users(UserId)
                );");
                    System.Diagnostics.Debug.WriteLine("Shifts table checked/created.");

                    // --- Таблица Операций с Денежным Ящиком ---
                    connection.Execute(@"
                CREATE TABLE IF NOT EXISTS CashDrawerOperations (
                    OperationId INTEGER PRIMARY KEY AUTOINCREMENT,
                    ShiftId INTEGER NOT NULL,
                    UserId INTEGER NOT NULL,
                    Timestamp DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
                    OperationType TEXT NOT NULL CHECK(OperationType IN ('CashIn', 'CashOut')),
                    Amount REAL NOT NULL,
                    Reason TEXT NULL,
                    FOREIGN KEY (ShiftId) REFERENCES Shifts(ShiftId),
                    FOREIGN KEY (UserId) REFERENCES Users(UserId)
                );");
                    System.Diagnostics.Debug.WriteLine("CashDrawerOperations table checked/created.");

                    // --- Таблица Чеков --- (С добавленной ShiftId)
                    connection.Execute(@"
                CREATE TABLE IF NOT EXISTS Checks (
                    CheckId INTEGER PRIMARY KEY AUTOINCREMENT,
                    ShiftId INTEGER NOT NULL,               -- <<--- Связь со сменой
                    CheckNumber INTEGER NOT NULL,           -- Номер чека в смене? Или глобальный?
                    Timestamp DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
                    UserId INTEGER NOT NULL,
                    TotalAmount REAL NOT NULL,
                    PaymentType TEXT NOT NULL CHECK(PaymentType IN ('Cash', 'Card', 'Mixed')),
                    CashPaid REAL DEFAULT 0,
                    CardPaid REAL DEFAULT 0,
                    DiscountAmount REAL DEFAULT 0,
                    IsReturn INTEGER NOT NULL DEFAULT 0,
                    OriginalCheckId INTEGER NULL,
                    FOREIGN KEY (UserId) REFERENCES Users(UserId),
                    FOREIGN KEY (ShiftId) REFERENCES Shifts(ShiftId) -- <<--- Связь со сменой
                );");
                    System.Diagnostics.Debug.WriteLine("Checks table checked/created.");

                    // --- Таблица Позиций Чека --- (Без изменений структуры)
                    connection.Execute(@"
                CREATE TABLE IF NOT EXISTS CheckItems (
                    CheckItemId INTEGER PRIMARY KEY AUTOINCREMENT,
                    CheckId INTEGER NOT NULL,
                    ProductId INTEGER NOT NULL,
                    Quantity REAL NOT NULL,
                    PriceAtSale REAL NOT NULL,
                    ItemTotalAmount REAL NOT NULL,
                    DiscountAmount REAL DEFAULT 0,
                    MarkingCode TEXT NULL,
                    FOREIGN KEY (CheckId) REFERENCES Checks(CheckId) ON DELETE CASCADE,
                    FOREIGN KEY (ProductId) REFERENCES Products(ProductId)
                );");
                    System.Diagnostics.Debug.WriteLine("CheckItems table checked/created.");

                    // --- Таблица Акций/Скидок --- (Пример)
                    connection.Execute(@"
                CREATE TABLE IF NOT EXISTS Discounts (
                    DiscountId INTEGER PRIMARY KEY AUTOINCREMENT,
                    Name TEXT NOT NULL,
                    Type TEXT NOT NULL CHECK(Type IN ('Percentage', 'FixedAmount', 'Gift')),
                    Value REAL NULL,
                    RequiredProductId INTEGER NULL,
                    GiftProductId INTEGER NULL,
                    StartDate DATETIME NULL,
                    EndDate DATETIME NULL,
                    IsActive INTEGER NOT NULL DEFAULT 1,
                    FOREIGN KEY (RequiredProductId) REFERENCES Products(ProductId),
                    FOREIGN KEY (GiftProductId) REFERENCES Products(ProductId)
                );");
                    System.Diagnostics.Debug.WriteLine("Discounts table checked/created.");

                    // Здесь можно добавить создание начальных данных, если нужно
                    // Например, пользователя Admin
                    // CreateDefaultAdminUser(connection);

                }
                catch (Exception ex) // Ловим более общее исключение
                {
                    System.Diagnostics.Debug.WriteLine($"Database initialization error: {ex.Message}\n{ex.StackTrace}");
                    // В реальном приложении - логирование и, возможно, сообщение пользователю
                    MessageBox.Show($"Ошибка инициализации базы данных: {ex.Message}", "Критическая ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
                    // Возможно, стоит завершить приложение, если БД не создалась
                    Application.Current.Shutdown();
                    // throw; // Можно не пробрасывать, если обработали и завершаем
                }
            }
        }

        // Пример добавления админа по умолчанию (вызывать из CreateTables, если нужно)
        private static void CreateDefaultAdminUser(SQLiteConnection connection)
        {
            // Проверить, существует ли уже админ
            var adminExists = connection.QueryFirstOrDefault<int>(
                "SELECT COUNT(*) FROM Users WHERE Username = @Username", new { Username = "admin" });

            if (adminExists == 0)
            {
                // Важно: Используйте безопасное хеширование паролей!
                // Это ПРОСТОЙ ПРИМЕР, НЕ ИСПОЛЬЗУЙТЕ В ПРОДАШКЕНЕ!
                // Замените на вызов вашего PasswordHasher.cs
                string hashedPassword = Utils.PasswordHasher.HashPassword("admin"); // Предполагается, что у вас есть такой класс/метод

                connection.Execute(@"
                INSERT INTO Users (Username, HashedPassword, FullName, Role)
                VALUES (@Username, @HashedPassword, @FullName, @Role);",
                    new
                    {
                        Username = "admin",
                        HashedPassword = hashedPassword,
                        FullName = "Администратор",
                        Role = "Admin"
                    });
                System.Diagnostics.Debug.WriteLine("Default admin user created.");
            }
        }
    }
}
