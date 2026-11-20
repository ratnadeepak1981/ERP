using System;
using System.Collections.Generic;
using Microsoft.Data.SqlClient;

namespace ERP.SecurityTests.DatabaseSync;

/// <summary>
/// Enforces strict database safety policies for automated database structure and bootstrap synchronisation tests.
/// Ensures tests can ONLY target isolated databases whose names strictly end with '_Test' and never touches
/// development or production databases.
/// </summary>
public static class DatabaseSyncSafetyGuard
{
    private static readonly HashSet<string> ProtectedDatabaseNames = new(StringComparer.OrdinalIgnoreCase)
    {
        "PlatformDB",
        "SecurityDB",
        "DomainDB",
        "AIDatabase",
        "master",
        "model",
        "msdb",
        "tempdb"
    };

    public const string PlatformDbTestConnection = "Server=(localdb)\\MSSQLLocalDB;Database=PlatformDB_Test;Trusted_Connection=True;TrustServerCertificate=True;";
    public const string SecurityDbTestConnection = "Server=(localdb)\\MSSQLLocalDB;Database=SecurityDB_Test;Trusted_Connection=True;TrustServerCertificate=True;";
    public const string DomainDbTestConnection = "Server=(localdb)\\MSSQLLocalDB;Database=DomainDB_Test;Trusted_Connection=True;TrustServerCertificate=True;";
    public const string AIDbTestConnection = "Server=(localdb)\\MSSQLLocalDB;Database=AIDatabase_Test;Trusted_Connection=True;TrustServerCertificate=True;";

    public static void AssertSafeTestDatabase(string connectionString)
    {
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            throw new InvalidOperationException("Connection string cannot be null or empty.");
        }

        var builder = new SqlConnectionStringBuilder(connectionString);
        string dbName = builder.InitialCatalog;

        if (string.IsNullOrWhiteSpace(dbName))
        {
            throw new InvalidOperationException("SAFETY VIOLATION: Database name (Initial Catalog) is missing from connection string.");
        }

        if (ProtectedDatabaseNames.Contains(dbName))
        {
            throw new InvalidOperationException(
                $"SAFETY VIOLATION: Database '{dbName}' is a PROTECTED development/system database! " +
                "Automated tests are strictly forbidden from targeting this database.");
        }

        if (!dbName.EndsWith("_Test", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException(
                $"SAFETY VIOLATION: Database '{dbName}' does not end with '_Test'! " +
                "All automated database sync and bootstrap tests must strictly target '*_Test' databases.");
        }
    }

    public static void EnsureDatabaseExists(string connectionString)
    {
        AssertSafeTestDatabase(connectionString);

        var builder = new SqlConnectionStringBuilder(connectionString);
        string dbName = builder.InitialCatalog;

        var masterBuilder = new SqlConnectionStringBuilder(connectionString)
        {
            InitialCatalog = "master"
        };

        using var conn = new SqlConnection(masterBuilder.ConnectionString);
        conn.Open();

        using var checkCmd = conn.CreateCommand();
        checkCmd.CommandText = $"SELECT database_id FROM sys.databases WHERE name = '{dbName.Replace("'", "''")}';";
        var result = checkCmd.ExecuteScalar();

        if (result == null || result == DBNull.Value)
        {
            using var createCmd = conn.CreateCommand();
            createCmd.CommandText = $"CREATE DATABASE [{dbName.Replace("]", "]]")}];";
            createCmd.ExecuteNonQuery();
        }
    }
}
