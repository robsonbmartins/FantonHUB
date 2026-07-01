using System.Collections.Generic;
using Microsoft.Data.Sqlite;
using System.IO;
using Serilog;
using System;

namespace CmcMidiRouter;

public static class DatabaseManager
{
    private static readonly string DbDirectory = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "CmcMidiRouter");
    private static readonly string DbFileName = Path.Combine(DbDirectory, "cmc_router.sqlite");
    private static string connectionString = $"Data Source={DbFileName};";

    static DatabaseManager()
    {
        if (!Directory.Exists(DbDirectory))
        {
            Directory.CreateDirectory(DbDirectory);
        }
    }
    public static void InitializeDatabase()
    {
        if (!File.Exists(DbFileName))
        {
            Log.Information("Criando banco de dados SQLite para perfis e configurações...");
        }

        using (var conn = new SqliteConnection(connectionString))
        {
            conn.Open();
            string createTableQuery = @"
                CREATE TABLE IF NOT EXISTS Dispositivos (
                    VID TEXT,
                    PID TEXT,
                    Serial TEXT,
                    Perfil TEXT,
                    PRIMARY KEY (VID, PID, Serial)
                )";
            using (var cmd = new SqliteCommand(createTableQuery, conn)) { cmd.ExecuteNonQuery(); }

            string createPortsTable = @"CREATE TABLE IF NOT EXISTS VirtualPorts (PortName TEXT PRIMARY KEY)";
            using (var cmd = new SqliteCommand(createPortsTable, conn)) { cmd.ExecuteNonQuery(); }

            string createRoutesTable = @"CREATE TABLE IF NOT EXISTS Routes (SourcePort TEXT, DestPort TEXT, PRIMARY KEY(SourcePort, DestPort))";
            using (var cmd = new SqliteCommand(createRoutesTable, conn)) { cmd.ExecuteNonQuery(); }

            string createPhysicalPortsTable = @"CREATE TABLE IF NOT EXISTS PhysicalPorts (PortName TEXT, IsInput INTEGER, PRIMARY KEY(PortName, IsInput))";
            using (var cmd = new SqliteCommand(createPhysicalPortsTable, conn)) { cmd.ExecuteNonQuery(); }

            // Insert default ports
            using (var countCmd = new SqliteCommand("SELECT COUNT(*) FROM VirtualPorts", conn))
            {
                long count = (long)countCmd.ExecuteScalar();
                if (count == 0)
                {
                    Log.Information("Populando banco de dados com portas virtuais padrão...");
                    using (var insertCmd = new SqliteCommand("INSERT INTO VirtualPorts (PortName) VALUES ('VM_MASTER_IN')", conn)) { insertCmd.ExecuteNonQuery(); }
                    using (var insertCmd = new SqliteCommand("INSERT INTO VirtualPorts (PortName) VALUES ('VM_MASTER_OUT')", conn)) { insertCmd.ExecuteNonQuery(); }
                }
            }
        }
    }

    public static List<string> GetVirtualPorts()
    {
        var ports = new List<string>();
        using (var conn = new SqliteConnection(connectionString))
        {
            conn.Open();
            using (var cmd = new SqliteCommand("SELECT PortName FROM VirtualPorts", conn))
            using (var reader = cmd.ExecuteReader())
            {
                while (reader.Read()) ports.Add(reader.GetString(0));
            }
        }
        return ports;
    }

    public static void AddVirtualPort(string portName)
    {
        using (var conn = new SqliteConnection(connectionString))
        {
            conn.Open();
            using (var cmd = new SqliteCommand("INSERT OR IGNORE INTO VirtualPorts (PortName) VALUES (@p)", conn))
            {
                cmd.Parameters.AddWithValue("@p", portName);
                cmd.ExecuteNonQuery();
            }
        }
    }

    public static void RemoveVirtualPort(string portName)
    {
        using (var conn = new SqliteConnection(connectionString))
        {
            conn.Open();
            using (var cmd = new SqliteCommand("DELETE FROM VirtualPorts WHERE PortName = @p", conn))
            {
                cmd.Parameters.AddWithValue("@p", portName);
                cmd.ExecuteNonQuery();
            }
            // Also remove routes associated with this port
            using (var cmd = new SqliteCommand("DELETE FROM Routes WHERE SourcePort = @p OR DestPort = @p", conn))
            {
                cmd.Parameters.AddWithValue("@p", portName);
                cmd.ExecuteNonQuery();
            }
        }
    }

    public static List<Tuple<string, string>> GetRoutes()
    {
        var routes = new List<Tuple<string, string>>();
        using (var conn = new SqliteConnection(connectionString))
        {
            conn.Open();
            using (var cmd = new SqliteCommand("SELECT SourcePort, DestPort FROM Routes", conn))
            using (var reader = cmd.ExecuteReader())
            {
                while (reader.Read()) routes.Add(new Tuple<string, string>(reader.GetString(0), reader.GetString(1)));
            }
        }
        return routes;
    }

    public static void AddRoute(string sourcePort, string destPort)
    {
        using (var conn = new SqliteConnection(connectionString))
        {
            conn.Open();
            using (var cmd = new SqliteCommand("INSERT OR IGNORE INTO Routes (SourcePort, DestPort) VALUES (@s, @d)", conn))
            {
                cmd.Parameters.AddWithValue("@s", sourcePort);
                cmd.Parameters.AddWithValue("@d", destPort);
                cmd.ExecuteNonQuery();
            }
        }
    }

    public static void RemoveRoute(string sourcePort, string destPort)
    {
        using (var conn = new SqliteConnection(connectionString))
        {
            conn.Open();
            using (var cmd = new SqliteCommand("DELETE FROM Routes WHERE SourcePort = @s AND DestPort = @d", conn))
            {
                cmd.Parameters.AddWithValue("@s", sourcePort);
                cmd.Parameters.AddWithValue("@d", destPort);
                cmd.ExecuteNonQuery();
            }
        }
    }

    public static void SaveAllRoutes(List<Tuple<string, string>> routes)
    {
        using (var conn = new SqliteConnection(connectionString))
        {
            conn.Open();
            using (var transaction = conn.BeginTransaction())
            {
                using (var cmd = new SqliteCommand("DELETE FROM Routes", conn, transaction))
                {
                    cmd.ExecuteNonQuery();
                }

                foreach (var route in routes)
                {
                    using (var cmd = new SqliteCommand("INSERT OR IGNORE INTO Routes (SourcePort, DestPort) VALUES (@s, @d)", conn, transaction))
                    {
                        cmd.Parameters.AddWithValue("@s", route.Item1);
                        cmd.Parameters.AddWithValue("@d", route.Item2);
                        cmd.ExecuteNonQuery();
                    }
                }
                transaction.Commit();
            }
        }
    }

    public static List<string> GetEnabledPhysicalPorts(bool isInput)
    {
        var ports = new List<string>();
        using (var conn = new SqliteConnection(connectionString))
        {
            conn.Open();
            using (var cmd = new SqliteCommand("SELECT PortName FROM PhysicalPorts WHERE IsInput = @isInput", conn))
            {
                cmd.Parameters.AddWithValue("@isInput", isInput ? 1 : 0);
                using (var reader = cmd.ExecuteReader())
                {
                    while (reader.Read()) ports.Add(reader.GetString(0));
                }
            }
        }
        return ports;
    }

    public static void SetEnabledPhysicalPorts(List<string> ports, bool isInput)
    {
        using (var conn = new SqliteConnection(connectionString))
        {
            conn.Open();
            using (var transaction = conn.BeginTransaction())
            {
                using (var cmd = new SqliteCommand("DELETE FROM PhysicalPorts WHERE IsInput = @isInput", conn, transaction))
                {
                    cmd.Parameters.AddWithValue("@isInput", isInput ? 1 : 0);
                    cmd.ExecuteNonQuery();
                }

                foreach (var p in ports)
                {
                    using (var cmd = new SqliteCommand("INSERT OR IGNORE INTO PhysicalPorts (PortName, IsInput) VALUES (@p, @isInput)", conn, transaction))
                    {
                        cmd.Parameters.AddWithValue("@p", p);
                        cmd.Parameters.AddWithValue("@isInput", isInput ? 1 : 0);
                        cmd.ExecuteNonQuery();
                    }
                }
                transaction.Commit();
            }
        }
    }
}

