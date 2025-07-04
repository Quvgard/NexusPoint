﻿using Dapper;
using System;
using System.Data.SQLite;
using System.Windows;

namespace NexusPoint.Data
{
    public static class DatabaseInitializer
    {
        public static void CreateTables()
        {
            using (var connection = DatabaseHelper.GetConnection())
            {
                try
                {
                    connection.Execute(@"
                    CREATE TABLE IF NOT EXISTS Users (
                        UserId INTEGER PRIMARY KEY AUTOINCREMENT,
                        Username TEXT NOT NULL UNIQUE,
                        HashedPassword TEXT NOT NULL,
                        FullName TEXT NOT NULL,
                        Role TEXT NOT NULL CHECK(Role IN ('Cashier', 'Manager', 'Admin'))
                    );");
                    System.Diagnostics.Debug.WriteLine("Users table checked/created.");
                    connection.Execute(@"
                    CREATE TABLE IF NOT EXISTS Products (
                        ProductId INTEGER PRIMARY KEY AUTOINCREMENT,
                        Barcode TEXT UNIQUE,
                        ProductCode TEXT NOT NULL UNIQUE,
                        Name TEXT NOT NULL,
                        Description TEXT NULL,   
                        Price REAL NOT NULL
                    );");
                    System.Diagnostics.Debug.WriteLine("Products (Catalog) table checked/created.");
                    connection.Execute(@"
                    CREATE TABLE IF NOT EXISTS StockItems (
                        StockItemId INTEGER PRIMARY KEY AUTOINCREMENT,
                        ProductId INTEGER NOT NULL UNIQUE,      -- Один товар - одна запись остатка
                        Quantity REAL NOT NULL DEFAULT 0,       -- Используем REAL для гибкости
                        LastUpdated DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
                        FOREIGN KEY (ProductId) REFERENCES Products(ProductId) ON DELETE CASCADE
                    );");
                    System.Diagnostics.Debug.WriteLine("StockItems table checked/created.");
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
                    connection.Execute(@"
                    CREATE TABLE IF NOT EXISTS CheckItems (
                        CheckItemId INTEGER PRIMARY KEY AUTOINCREMENT,
                        CheckId INTEGER NOT NULL,
                        ProductId INTEGER NOT NULL,
                        Quantity REAL NOT NULL,
                        PriceAtSale REAL NOT NULL,
                        ItemTotalAmount REAL NOT NULL,
                        DiscountAmount REAL DEFAULT 0,
                        AppliedDiscountId INTEGER NULL, 
                        FOREIGN KEY (CheckId) REFERENCES Checks(CheckId) ON DELETE CASCADE,
                        FOREIGN KEY (ProductId) REFERENCES Products(ProductId),
                        FOREIGN KEY (AppliedDiscountId) REFERENCES Discounts(DiscountId) ON DELETE SET NULL 
                    );");
                    System.Diagnostics.Debug.WriteLine("CheckItems table checked/created.");
                    connection.Execute(@"
                    CREATE TABLE Discounts (
                        DiscountId INTEGER PRIMARY KEY AUTOINCREMENT,
                        Name TEXT NOT NULL,
                        Type TEXT NOT NULL CHECK(Type IN ('Процент', 'Сумма', 'Подарок', 'Фикс. цена', 'N+M Подарок', 'Скидка на N-ный', 'Скидка на сумму чека')),
                        Description TEXT NULL, 
                        IsActive INTEGER NOT NULL DEFAULT 1,
                        StartDate DATETIME NULL,
                        EndDate DATETIME NULL,
                        Value REAL NULL,                    -- Для % / Сумма / Фикс.цена / СкидкаНаN(сумма) / СкидкаНаЧек(сумма)
                        RequiredProductId INTEGER NULL,     -- FK к Products
                        GiftProductId INTEGER NULL,         -- FK к Products
                        RequiredQuantityN INTEGER NULL,     -- Для N+M
                        GiftQuantityM INTEGER NULL,         -- Для N+M
                        NthItemNumber INTEGER NULL,         -- Для СкидкаНаN
                        IsNthDiscountPercentage INTEGER NOT NULL DEFAULT 0, -- Для СкидкаНаN (0=сумма, 1=процент)
                        CheckAmountThreshold REAL NULL,     -- Для СкидкаНаЧек
                        IsCheckDiscountPercentage INTEGER NOT NULL DEFAULT 0, -- Для СкидкаНаЧек (0=сумма, 1=процент)
                        FOREIGN KEY (RequiredProductId) REFERENCES Products(ProductId) ON DELETE SET NULL, -- При удалении товара сбрасываем ссылку
                        FOREIGN KEY (GiftProductId) REFERENCES Products(ProductId) ON DELETE SET NULL
                    );");
                    System.Diagnostics.Debug.WriteLine("Discounts table re-created.");
                    CreateDefaultAdminUser(connection);

                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Database initialization error: {ex.Message}\n{ex.StackTrace}");
                    MessageBox.Show($"Ошибка инициализации базы данных: {ex.Message}", "Критическая ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
                    Application.Current.Shutdown();
                }
            }
        }
        private static void CreateDefaultAdminUser(SQLiteConnection connection)
        {
            var userExists = connection.QueryFirstOrDefault<int>(
                "SELECT COUNT(*) FROM Users WHERE Username = @Username", new { Username = "admin" });

            if (userExists == 0)
            {
                if (typeof(Utils.PasswordHasher).GetMethod("HashPassword") == null)
                {
                    System.Diagnostics.Debug.WriteLine("ERROR: PasswordHasher.HashPassword method not found. Cannot create default user.");
                    MessageBox.Show("Не найден метод хеширования пароля. Пользователь по умолчанию не создан.", "Ошибка инициализации", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                string defaultUsername = "admin";
                string defaultPassword = "admin";
                string hashedPassword = Utils.PasswordHasher.HashPassword(defaultPassword);

                connection.Execute(@"
                INSERT INTO Users (Username, HashedPassword, FullName, Role)
                VALUES (@Username, @HashedPassword, @FullName, @Role);",
                    new
                    {
                        Username = defaultUsername,
                        HashedPassword = hashedPassword,
                        FullName = "Администратор Системы",
                        Role = "Admin"
                    });
                System.Diagnostics.Debug.WriteLine($"Default admin user created (Username: {defaultUsername}, Password: {defaultPassword}).");
                MessageBox.Show($"Создана учетная запись администратора по умолчанию:\nЛогин: {defaultUsername}\nПароль: {defaultPassword}\n\nРекомендуется сменить пароль после первого входа.",
                                "Пользователь по умолчанию", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            else
            {
                System.Diagnostics.Debug.WriteLine("Admin user already exists or other users found.");
            }
        }
    }
}
