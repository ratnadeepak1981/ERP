using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;

namespace ERP.SecurityTests.DatabaseSync;

public class SchemaComparisonResult
{
    public string DatabaseName { get; set; } = string.Empty;
    public int DatabaseTableCount { get; set; }
    public int ModelTableCount { get; set; }
    public int DatabaseColumnCount { get; set; }
    public int IndexCount { get; set; }
    public int ForeignKeyCount { get; set; }
    public int PrimaryKeyCount { get; set; }
    public List<string> Discrepancies { get; set; } = new();
    public bool IsMatch => Discrepancies.Count == 0;
}

public static class DatabaseSchemaInspector
{
    public static SchemaComparisonResult Compare(DbContext context, string connectionString)
    {
        DatabaseSyncSafetyGuard.AssertSafeTestDatabase(connectionString);

        var builder = new SqlConnectionStringBuilder(connectionString);
        string dbName = builder.InitialCatalog;

        var result = new SchemaComparisonResult
        {
            DatabaseName = dbName
        };

        using var connection = new SqlConnection(connectionString);
        connection.Open();

        // 1. Physical tables from INFORMATION_SCHEMA
        var dbTables = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        using (var cmd = connection.CreateCommand())
        {
            cmd.CommandText = "SELECT TABLE_NAME FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_TYPE = 'BASE TABLE' AND TABLE_NAME != '__EFMigrationsHistory';";
            using var reader = cmd.ExecuteReader();
            while (reader.Read())
            {
                dbTables.Add(reader.GetString(0));
            }
        }
        result.DatabaseTableCount = dbTables.Count;

        // 2. Physical columns from INFORMATION_SCHEMA
        var dbColumns = new Dictionary<string, HashSet<string>>(StringComparer.OrdinalIgnoreCase);
        var dbColumnNullability = new Dictionary<string, bool>(StringComparer.OrdinalIgnoreCase);
        int totalCols = 0;
        using (var cmd = connection.CreateCommand())
        {
            cmd.CommandText = "SELECT TABLE_NAME, COLUMN_NAME, IS_NULLABLE FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME != '__EFMigrationsHistory';";
            using var reader = cmd.ExecuteReader();
            while (reader.Read())
            {
                string table = reader.GetString(0);
                string col = reader.GetString(1);
                bool isNullable = string.Equals(reader.GetString(2), "YES", StringComparison.OrdinalIgnoreCase);

                if (!dbColumns.TryGetValue(table, out var colSet))
                {
                    colSet = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                    dbColumns[table] = colSet;
                }
                colSet.Add(col);
                dbColumnNullability[$"{table}.{col}"] = isNullable;
                totalCols++;
            }
        }
        result.DatabaseColumnCount = totalCols;

        // 3. Primary keys
        using (var cmd = connection.CreateCommand())
        {
            cmd.CommandText = "SELECT COUNT(DISTINCT TABLE_NAME) FROM INFORMATION_SCHEMA.TABLE_CONSTRAINTS WHERE CONSTRAINT_TYPE = 'PRIMARY KEY' AND TABLE_NAME != '__EFMigrationsHistory';";
            var pkCount = cmd.ExecuteScalar();
            result.PrimaryKeyCount = pkCount is int i ? i : Convert.ToInt32(pkCount);
        }

        // 4. Foreign keys
        using (var cmd = connection.CreateCommand())
        {
            cmd.CommandText = "SELECT COUNT(*) FROM sys.foreign_keys;";
            var fkCount = cmd.ExecuteScalar();
            result.ForeignKeyCount = fkCount is int i ? i : Convert.ToInt32(fkCount);
        }

        // 5. Indexes
        using (var cmd = connection.CreateCommand())
        {
            cmd.CommandText = "SELECT COUNT(*) FROM sys.indexes i INNER JOIN sys.objects o ON i.object_id = o.object_id WHERE o.type = 'U' AND o.name != '__EFMigrationsHistory' AND i.type > 0;";
            var idxCount = cmd.ExecuteScalar();
            result.IndexCount = idxCount is int i ? i : Convert.ToInt32(idxCount);
        }

        // 6. Compare with EF Core Model
        var modelTables = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var entityTypes = context.Model.GetEntityTypes()
            .Where(e => !e.IsOwned() && e.GetTableName() != null)
            .ToList();

        foreach (var entityType in entityTypes)
        {
            string? tableName = entityType.GetTableName();
            if (string.IsNullOrEmpty(tableName)) continue;

            modelTables.Add(tableName);

            // Verify table existence in DB
            if (!dbTables.Contains(tableName))
            {
                result.Discrepancies.Add($"Missing table in database '{dbName}': '{tableName}' expected by entity '{entityType.ClrType.Name}'.");
                continue;
            }

            // Verify column existence & nullability
            if (dbColumns.TryGetValue(tableName, out var cols))
            {
                foreach (var prop in entityType.GetProperties())
                {
                    string? colName = prop.GetColumnName();
                    if (string.IsNullOrEmpty(colName)) continue;

                    if (!cols.Contains(colName))
                    {
                        result.Discrepancies.Add($"Missing column in database '{dbName}.{tableName}': '{colName}' expected by property '{prop.Name}'.");
                    }
                    else
                    {
                        string colKey = $"{tableName}.{colName}";
                        if (dbColumnNullability.TryGetValue(colKey, out bool dbNullable))
                        {
                            // If EF model property is required (IsNullable == false) but DB column is nullable, or vice versa
                            // Only report if EF requires it but DB doesn't or vice versa in critical mismatch
                            if (!prop.IsNullable && dbNullable && !prop.IsPrimaryKey())
                            {
                                // Warning / discrepancy
                                result.Discrepancies.Add($"Nullability mismatch in '{dbName}.{tableName}.{colName}': Model requires NOT NULL, but database allows NULL.");
                            }
                        }
                    }
                }
            }
        }

        result.ModelTableCount = modelTables.Count;

        return result;
    }

    public static List<string> CheckForCrossDatabaseForeignKeys(string connectionString)
    {
        DatabaseSyncSafetyGuard.AssertSafeTestDatabase(connectionString);

        var list = new List<string>();
        using var connection = new SqlConnection(connectionString);
        connection.Open();

        using var cmd = connection.CreateCommand();
        // Check if any FK refers to an external database
        cmd.CommandText = @"
            SELECT fk.name AS FK_Name, OBJECT_NAME(fk.parent_object_id) AS TableName
            FROM sys.foreign_keys fk
            WHERE fk.referenced_object_id IN (
                SELECT object_id FROM sys.objects WHERE type = 'U'
            );";

        using var reader = cmd.ExecuteReader();
        while (reader.Read())
        {
            // All normal FKs refer to tables in the current DB. In SQL Server, cross-database FKs are physically impossible.
        }

        return list; // SQL Server does not support cross-database foreign keys; returns empty.
    }
}
