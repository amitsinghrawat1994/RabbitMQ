using System;
using Microsoft.Data.Sqlite;

var dbPath = args.Length > 0 ? args[0] : "OrderService/orders_v2.db";
if (!System.IO.File.Exists(dbPath))
{
    Console.WriteLine($"DB file not found: {dbPath}");
    return 1;
}

using var connection = new SqliteConnection($"Data Source={dbPath}");
connection.Open();

using var cmd = connection.CreateCommand();

cmd.CommandText = "SELECT name FROM sqlite_master WHERE type='table' ORDER BY name;";
using (var reader1 = cmd.ExecuteReader())
{
    Console.WriteLine("Tables:");
    while (reader1.Read())
    {
        Console.WriteLine(" - " + reader1.GetString(0));
    }
}

cmd.CommandText = "SELECT name FROM sqlite_master WHERE type='table' AND name LIKE 'Saga%';";
using (var reader2 = cmd.ExecuteReader())
{
    Console.WriteLine("\nSaga-like tables:");
    while (reader2.Read())
    {
        Console.WriteLine(" - " + reader2.GetString(0));
    }
}

// Try select from the OrderState table if exists
cmd.CommandText = "SELECT name FROM sqlite_master WHERE type='table' AND name='OrderState';";
using (var reader3 = cmd.ExecuteReader())
{
    if (reader3.Read())
    {
        // Print table schema
        Console.WriteLine("\nOrderState schema:");
        using (var cmdSchema = connection.CreateCommand())
        {
            cmdSchema.CommandText = "PRAGMA table_info('OrderState');";
            using var rs = cmdSchema.ExecuteReader();
            while (rs.Read())
            {
                Console.WriteLine($" - {rs.GetString(1)} ({rs.GetString(2)})");
            }
        }

        Console.WriteLine("\nReading OrderState rows:");
        using var cmd2 = connection.CreateCommand();
        cmd2.CommandText = "SELECT CorrelationId, CurrentState, Created, Updated, CustomerNumber, TotalAmount FROM OrderState;";
        using var r2 = cmd2.ExecuteReader();
        while (r2.Read())
        {
            Console.WriteLine($"Id={r2.GetString(0)}, State={r2.GetString(1)}, Customer={(r2.IsDBNull(4) ? "(null)" : r2.GetString(4))}, Amount={r2.GetValue(5)}, Created={r2.GetValue(2)}, Updated={r2.GetValue(3)}");
        }
    }
    else
    {
        Console.WriteLine("OrderState table not found.");
    }
}

return 0;