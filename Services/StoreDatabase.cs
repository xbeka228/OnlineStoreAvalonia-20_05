using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.Data.Sqlite;
using OnlineStoreAvalonia.Models;

namespace OnlineStoreAvalonia.Services;

public sealed class StoreDatabase
{
    private readonly string _connectionString;

    public StoreDatabase(string? databasePath = null)
    {
        databasePath ??= GetDefaultDatabasePath();
        Directory.CreateDirectory(Path.GetDirectoryName(databasePath)!);

        _connectionString = new SqliteConnectionStringBuilder
        {
            DataSource = Path.GetFullPath(databasePath)
        }.ToString();

        Initialize();
    }

    private static string GetDefaultDatabasePath()
    {
        var appData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        if (string.IsNullOrWhiteSpace(appData))
        {
            appData = AppContext.BaseDirectory;
        }

        var dataDirectory = Path.Combine(appData, "OnlineStoreAvalonia");
        var databasePath = Path.Combine(dataDirectory, "online_store.db");
        var oldDatabasePath = Path.Combine(AppContext.BaseDirectory, "online_store.db");

        if (!File.Exists(databasePath) && File.Exists(oldDatabasePath))
        {
            Directory.CreateDirectory(dataDirectory);
            File.Copy(oldDatabasePath, databasePath);
        }

        return databasePath;
    }

    public IReadOnlyList<Product> GetProducts(string? searchText, string? category)
    {
        using var connection = OpenConnection();
        using var command = connection.CreateCommand();

        command.CommandText = """
            SELECT Id, Name, Category, Description, Price, Stock, ImagePath
            FROM Products
            WHERE (@category = '' OR Category = @category)
              AND (@search = '' OR Name LIKE '%' || @search || '%' OR Description LIKE '%' || @search || '%')
            ORDER BY Category, Name;
            """;
        command.Parameters.AddWithValue("@category", category ?? string.Empty);
        command.Parameters.AddWithValue("@search", searchText ?? string.Empty);

        using var reader = command.ExecuteReader();
        var products = new List<Product>();

        while (reader.Read())
        {
            products.Add(new Product
            {
                Id = reader.GetInt32(0),
                Name = reader.GetString(1),
                Category = reader.GetString(2),
                Description = reader.GetString(3),
                Price = reader.GetDecimal(4),
                Stock = reader.GetInt32(5),
                ImagePath = reader.IsDBNull(6) ? string.Empty : reader.GetString(6)
            });
        }

        return products;
    }

    public IReadOnlyList<string> GetCategories()
    {
        using var connection = OpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText = "SELECT DISTINCT Category FROM Products ORDER BY Category;";

        using var reader = command.ExecuteReader();
        var categories = new List<string> { "Все" };

        while (reader.Read())
        {
            categories.Add(reader.GetString(0));
        }

        return categories;
    }

    public IReadOnlyList<Order> GetRecentOrders()
    {
        using var connection = OpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT Id, CustomerName, Phone, Address, Total, CreatedAt, IsApproved
            FROM Orders
            ORDER BY datetime(CreatedAt) DESC
            LIMIT 30;
            """;

        using var reader = command.ExecuteReader();
        var orders = new List<Order>();

        while (reader.Read())
        {
            orders.Add(new Order
            {
                Id = reader.GetInt32(0),
                CustomerName = reader.GetString(1),
                Phone = reader.GetString(2),
                Address = reader.GetString(3),
                Total = reader.GetDecimal(4),
                CreatedAt = DateTime.Parse(reader.GetString(5)),
                IsApproved = reader.GetInt32(6) == 1
            });
        }

        return orders;
    }

    public Order? GetOrder(int orderId)
    {
        using var connection = OpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT Id, CustomerName, Phone, Address, Total, CreatedAt, IsApproved
            FROM Orders
            WHERE Id = @orderId;
            """;
        command.Parameters.AddWithValue("@orderId", orderId);

        using var reader = command.ExecuteReader();
        if (!reader.Read())
        {
            return null;
        }

        var order = new Order
        {
            Id = reader.GetInt32(0),
            CustomerName = reader.GetString(1),
            Phone = reader.GetString(2),
            Address = reader.GetString(3),
            Total = reader.GetDecimal(4),
            CreatedAt = DateTime.Parse(reader.GetString(5)),
            IsApproved = reader.GetInt32(6) == 1,
            Lines = GetOrderLines(orderId)
        };

        return order;
    }

    public IReadOnlyList<OrderLine> GetOrderLines(int orderId)
    {
        using var connection = OpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT OrderId, ProductId, ProductName, Quantity, UnitPrice
            FROM OrderLines
            WHERE OrderId = @orderId
            ORDER BY Id;
            """;
        command.Parameters.AddWithValue("@orderId", orderId);

        using var reader = command.ExecuteReader();
        var lines = new List<OrderLine>();

        while (reader.Read())
        {
            lines.Add(new OrderLine
            {
                OrderId = reader.GetInt32(0),
                ProductId = reader.GetInt32(1),
                ProductName = reader.GetString(2),
                Quantity = reader.GetInt32(3),
                UnitPrice = reader.GetDecimal(4)
            });
        }

        return lines;
    }

    public void AddProduct(string name, string category, string description, decimal price, int stock, string imagePath = "")
    {
        using var connection = OpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText = """
            INSERT INTO Products (Name, Category, Description, Price, Stock, ImagePath)
            VALUES (@name, @category, @description, @price, @stock, @imagePath);
            """;
        command.Parameters.AddWithValue("@name", name.Trim());
        command.Parameters.AddWithValue("@category", category.Trim());
        command.Parameters.AddWithValue("@description", description.Trim());
        command.Parameters.AddWithValue("@price", price);
        command.Parameters.AddWithValue("@stock", stock);
        command.Parameters.AddWithValue("@imagePath", imagePath.Trim());
        command.ExecuteNonQuery();
    }

    public void DeleteProduct(int productId)
    {
        using var connection = OpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText = "DELETE FROM Products WHERE Id = @productId;";
        command.Parameters.AddWithValue("@productId", productId);
        command.ExecuteNonQuery();
    }

    public void ApproveOrder(int orderId)
    {
        using var connection = OpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText = "UPDATE Orders SET IsApproved = 1 WHERE Id = @orderId;";
        command.Parameters.AddWithValue("@orderId", orderId);
        command.ExecuteNonQuery();
    }

    public void DeleteOrder(int orderId)
    {
        using var connection = OpenConnection();
        using var transaction = connection.BeginTransaction();

        using var linesCommand = connection.CreateCommand();
        linesCommand.Transaction = transaction;
        linesCommand.CommandText = "DELETE FROM OrderLines WHERE OrderId = @orderId;";
        linesCommand.Parameters.AddWithValue("@orderId", orderId);
        linesCommand.ExecuteNonQuery();

        using var orderCommand = connection.CreateCommand();
        orderCommand.Transaction = transaction;
        orderCommand.CommandText = "DELETE FROM Orders WHERE Id = @orderId;";
        orderCommand.Parameters.AddWithValue("@orderId", orderId);
        orderCommand.ExecuteNonQuery();

        transaction.Commit();
    }

    public Customer? ValidateCustomer(string login, string password)
    {
        using var connection = OpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT Id, Login, FullName, Phone
            FROM Customers
            WHERE Login = @login AND Password = @password;
            """;
        command.Parameters.AddWithValue("@login", login.Trim());
        command.Parameters.AddWithValue("@password", password);

        using var reader = command.ExecuteReader();
        if (!reader.Read())
        {
            return null;
        }

        return new Customer
        {
            Id = reader.GetInt32(0),
            Login = reader.GetString(1),
            FullName = reader.GetString(2),
            Phone = reader.GetString(3)
        };
    }

    public bool CustomerLoginExists(string login)
    {
        using var connection = OpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText = "SELECT COUNT(*) FROM Customers WHERE Login = @login;";
        command.Parameters.AddWithValue("@login", login.Trim());

        return Convert.ToInt32(command.ExecuteScalar()) > 0;
    }

    public Customer RegisterCustomer(string login, string password, string fullName, string phone)
    {
        using var connection = OpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText = """
            INSERT INTO Customers (Login, Password, FullName, Phone, CreatedAt)
            VALUES (@login, @password, @fullName, @phone, @createdAt)
            RETURNING Id;
            """;
        command.Parameters.AddWithValue("@login", login.Trim());
        command.Parameters.AddWithValue("@password", password);
        command.Parameters.AddWithValue("@fullName", fullName.Trim());
        command.Parameters.AddWithValue("@phone", phone.Trim());
        command.Parameters.AddWithValue("@createdAt", DateTime.Now.ToString("O"));

        var customerId = Convert.ToInt32(command.ExecuteScalar());
        return new Customer
        {
            Id = customerId,
            Login = login.Trim(),
            FullName = fullName.Trim(),
            Phone = phone.Trim()
        };
    }

    public int CreateOrder(string customerName, string phone, string address, IReadOnlyCollection<CartItem> cartItems)
    {
        using var connection = OpenConnection();
        using var transaction = connection.BeginTransaction();

        var total = cartItems.Sum(item => item.LineTotal);
        using var orderCommand = connection.CreateCommand();
        orderCommand.Transaction = transaction;
        orderCommand.CommandText = """
            INSERT INTO Orders (CustomerName, Phone, Address, Total, CreatedAt, IsApproved)
            VALUES (@customerName, @phone, @address, @total, @createdAt, 0)
            RETURNING Id;
            """;
        orderCommand.Parameters.AddWithValue("@customerName", customerName.Trim());
        orderCommand.Parameters.AddWithValue("@phone", phone.Trim());
        orderCommand.Parameters.AddWithValue("@address", address.Trim());
        orderCommand.Parameters.AddWithValue("@total", total);
        orderCommand.Parameters.AddWithValue("@createdAt", DateTime.Now.ToString("O"));

        var orderId = Convert.ToInt32(orderCommand.ExecuteScalar());

        foreach (var item in cartItems)
        {
            using var lineCommand = connection.CreateCommand();
            lineCommand.Transaction = transaction;
            lineCommand.CommandText = """
                INSERT INTO OrderLines (OrderId, ProductId, ProductName, Quantity, UnitPrice)
                VALUES (@orderId, @productId, @productName, @quantity, @unitPrice);

                UPDATE Products
                SET Stock = Stock - @quantity
                WHERE Id = @productId;
                """;
            lineCommand.Parameters.AddWithValue("@orderId", orderId);
            lineCommand.Parameters.AddWithValue("@productId", item.Product.Id);
            lineCommand.Parameters.AddWithValue("@productName", item.Product.Name);
            lineCommand.Parameters.AddWithValue("@quantity", item.Quantity);
            lineCommand.Parameters.AddWithValue("@unitPrice", item.Product.Price);
            lineCommand.ExecuteNonQuery();
        }

        transaction.Commit();
        return orderId;
    }

    private SqliteConnection OpenConnection()
    {
        var connection = new SqliteConnection(_connectionString);
        connection.Open();
        return connection;
    }

    private void Initialize()
    {
        using var connection = OpenConnection();
        using var command = connection.CreateCommand();

        command.CommandText = """
            CREATE TABLE IF NOT EXISTS Products (
                Id INTEGER PRIMARY KEY AUTOINCREMENT,
                Name TEXT NOT NULL,
                Category TEXT NOT NULL,
                Description TEXT NOT NULL,
                Price REAL NOT NULL,
                Stock INTEGER NOT NULL,
                ImagePath TEXT NOT NULL DEFAULT ''
            );

            CREATE TABLE IF NOT EXISTS Customers (
                Id INTEGER PRIMARY KEY AUTOINCREMENT,
                Login TEXT NOT NULL UNIQUE,
                Password TEXT NOT NULL,
                FullName TEXT NOT NULL,
                Phone TEXT NOT NULL,
                CreatedAt TEXT NOT NULL
            );

            CREATE TABLE IF NOT EXISTS Orders (
                Id INTEGER PRIMARY KEY AUTOINCREMENT,
                CustomerName TEXT NOT NULL,
                Phone TEXT NOT NULL,
                Address TEXT NOT NULL,
                Total REAL NOT NULL,
                CreatedAt TEXT NOT NULL,
                IsApproved INTEGER NOT NULL DEFAULT 0
            );

            CREATE TABLE IF NOT EXISTS OrderLines (
                Id INTEGER PRIMARY KEY AUTOINCREMENT,
                OrderId INTEGER NOT NULL,
                ProductId INTEGER NOT NULL,
                ProductName TEXT NOT NULL,
                Quantity INTEGER NOT NULL,
                UnitPrice REAL NOT NULL,
                FOREIGN KEY (OrderId) REFERENCES Orders(Id),
                FOREIGN KEY (ProductId) REFERENCES Products(Id)
            );

            INSERT INTO Products (Name, Category, Description, Price, Stock)
            SELECT 'Ноутбук Lenovo IdeaPad', 'Компьютеры', '15.6 дюйм, 16 GB RAM, SSD 512 GB', 329990, 7
            WHERE NOT EXISTS (SELECT 1 FROM Products);

            INSERT INTO Products (Name, Category, Description, Price, Stock)
            SELECT 'Смартфон Samsung Galaxy', 'Смартфоны', 'AMOLED экран, 256 GB, быстрая зарядка', 249990, 10
            WHERE (SELECT COUNT(*) FROM Products) = 1;

            INSERT INTO Products (Name, Category, Description, Price, Stock)
            SELECT 'Наушники Sony WH-CH720N', 'Аксессуары', 'Беспроводные, активное шумоподавление', 59990, 18
            WHERE (SELECT COUNT(*) FROM Products) = 2;

            INSERT INTO Products (Name, Category, Description, Price, Stock)
            SELECT 'Монитор LG UltraWide', 'Компьютеры', '29 дюймов, IPS, USB-C', 154990, 6
            WHERE (SELECT COUNT(*) FROM Products) = 3;

            INSERT INTO Products (Name, Category, Description, Price, Stock)
            SELECT 'Клавиатура Logitech MX Keys', 'Аксессуары', 'Тихие клавиши, Bluetooth, подсветка', 44990, 14
            WHERE (SELECT COUNT(*) FROM Products) = 4;

            INSERT INTO Products (Name, Category, Description, Price, Stock)
            SELECT 'Планшет iPad Air', 'Планшеты', '10.9 дюйм, Wi-Fi, 128 GB', 319990, 5
            WHERE (SELECT COUNT(*) FROM Products) = 5;
            """;
        command.ExecuteNonQuery();
        EnsureOrderApprovalColumn(connection);
        EnsureProductImageColumn(connection);
        SeedAutoProducts(connection);
    }

    private static void EnsureOrderApprovalColumn(SqliteConnection connection)
    {
        using var checkCommand = connection.CreateCommand();
        checkCommand.CommandText = "PRAGMA table_info(Orders);";

        using var reader = checkCommand.ExecuteReader();
        while (reader.Read())
        {
            if (reader.GetString(1) == "IsApproved")
            {
                return;
            }
        }

        reader.Close();

        using var alterCommand = connection.CreateCommand();
        alterCommand.CommandText = "ALTER TABLE Orders ADD COLUMN IsApproved INTEGER NOT NULL DEFAULT 0;";
        alterCommand.ExecuteNonQuery();
    }

    private static void EnsureProductImageColumn(SqliteConnection connection)
    {
        using var checkCommand = connection.CreateCommand();
        checkCommand.CommandText = "PRAGMA table_info(Products);";

        using var reader = checkCommand.ExecuteReader();
        while (reader.Read())
        {
            if (reader.GetString(1) == "ImagePath")
            {
                return;
            }
        }

        reader.Close();

        using var alterCommand = connection.CreateCommand();
        alterCommand.CommandText = "ALTER TABLE Products ADD COLUMN ImagePath TEXT NOT NULL DEFAULT '';";
        alterCommand.ExecuteNonQuery();
    }

    private static void SeedAutoProducts(SqliteConnection connection)
    {
        using var countCommand = connection.CreateCommand();
        countCommand.CommandText = """
            SELECT COUNT(*)
            FROM Products
            WHERE Category IN ('Масла', 'Шины', 'Запчасти', 'Электрика');
            """;

        if (Convert.ToInt32(countCommand.ExecuteScalar()) > 0)
        {
            return;
        }

        using var cleanupCommand = connection.CreateCommand();
        cleanupCommand.CommandText = """
            DELETE FROM OrderLines;
            DELETE FROM Orders;
            DELETE FROM Products;
            DELETE FROM sqlite_sequence WHERE name IN ('Products', 'Orders', 'OrderLines');
            """;
        cleanupCommand.ExecuteNonQuery();

        using var seedCommand = connection.CreateCommand();
        seedCommand.CommandText = """
            INSERT INTO Products (Name, Category, Description, Price, Stock) VALUES
            ('Моторное масло 5W-30 Shell 4L', 'Масла', 'Синтетическое масло для бензиновых и дизельных двигателей', 18500, 20),
            ('Зимние шины Michelin X-Ice 205/55 R16', 'Шины', 'Комплектная продажа поштучно, усиленное сцепление на льду', 42900, 16),
            ('Аккумулятор Bosch S4 60Ah', 'Электрика', '12V, пусковой ток 540A, для легковых автомобилей', 38900, 9),
            ('Тормозные колодки Brembo передние', 'Запчасти', 'Комплект для популярных седанов, низкий уровень шума', 16900, 14),
            ('Воздушный фильтр Toyota Corolla', 'Запчасти', 'Оригинальный размер, защита двигателя от пыли', 6900, 25),
            ('Автомобильный компрессор 12V', 'Аксессуары', 'Манометр, фонарь, питание от прикуривателя', 12900, 12),
            ('Щетки стеклоочистителя Bosch 600/450', 'Аксессуары', 'Бескаркасные дворники для всесезонного использования', 7900, 18),
            ('Антифриз G12 красный 5L', 'Масла', 'Охлаждающая жидкость до -40 C', 8900, 22);
            """;
        seedCommand.ExecuteNonQuery();
    }
}
