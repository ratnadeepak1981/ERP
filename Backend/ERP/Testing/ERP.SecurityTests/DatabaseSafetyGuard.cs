using System;
using Microsoft.Data.SqlClient;

namespace ERP.SecurityTests;

public static class DatabaseSafetyGuard
{
    public static void AssertSafeTestDatabase(string connectionString)
    {
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            throw new InvalidOperationException("Connection string cannot be null or empty.");
        }

        var builder = new SqlConnectionStringBuilder(connectionString);
        string dbName = builder.InitialCatalog;

        if (string.IsNullOrWhiteSpace(dbName) || !dbName.EndsWith("_Test", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException(
                $"SAFETY VIOLATION: Database '{dbName}' is NOT an authorized test database! " +
                "Connection target must strictly end with '_Test' to prevent touching development or production data.");
        }
    }
}
