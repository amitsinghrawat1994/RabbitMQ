using System;
using Microsoft.Data.Sqlite;

class Program
{
    static void Main()
    {
        var path = "OrderService/orders_v2.db";
        using var conn = new SqliteConnection($"Data Source={path}");
        conn.Open();

        Console.WriteLine("Tables:");
        using (var cmd = conn.CreateCommand())
        {
            cmd.CommandText = "SELECT name, sql FROM sqlite_master WHERE type='table' ORDER BY name;";
            using var r = cmd.ExecuteReader();
            while (r.Read())
            {
                Console.WriteLine($"{r.GetString(0)} -> {r.GetString(1)}");
            }
        }

        Console.WriteLine("\nOrderState columns:");
        using (var cmd = conn.CreateCommand())
        {
            cmd.CommandText = "PRAGMA table_info('OrderState');";
            using var r = cmd.ExecuteReader();
            if (!r.HasRows)
            {
                Console.WriteLine("OrderState table not found");
            }
            else
            {
                while (r.Read())
                {
                    Console.WriteLine($"{r.GetInt32(0)}: {r.GetString(1)} ({r.GetString(2)})");
                }
            }
        }

        Console.WriteLine("\nMigrations history:");
        using (var cmd = conn.CreateCommand())
        {
            cmd.CommandText = "SELECT MigrationId, ProductVersion FROM __EFMigrationsHistory ORDER BY MigrationId;";
            try
            {
                using var r = cmd.ExecuteReader();
                while (r.Read())
                {
                    Console.WriteLine($"{r.GetString(0)} ({r.GetString(1)})");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine("Failed to read migrations history: " + ex.Message);
            }
        }

        conn.Close();
    }
}
