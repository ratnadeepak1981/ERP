using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;
using SaaS.Application.Interfaces;

namespace API.Services;

public class DomainDatabaseConfiguration
    : IDomainDatabaseConfiguration
{
    public string DatabaseName { get; }

    public string DatabaseServer { get; }

    public DomainDatabaseConfiguration(
        IConfiguration configuration)
    {
        string? connectionString =
            configuration.GetConnectionString(
                "DomainDatabase");

        if (string.IsNullOrWhiteSpace(connectionString))
        {
            throw new InvalidOperationException(
                "DomainDatabase connection string is not configured.");
        }

        SqlConnectionStringBuilder builder =
            new SqlConnectionStringBuilder(
                connectionString);

        if (string.IsNullOrWhiteSpace(builder.InitialCatalog))
        {
            throw new InvalidOperationException(
                "DomainDatabase does not contain a database name.");
        }

        if (string.IsNullOrWhiteSpace(builder.DataSource))
        {
            throw new InvalidOperationException(
                "DomainDatabase does not contain a database server.");
        }

        DatabaseName = builder.InitialCatalog;
        DatabaseServer = builder.DataSource;
    }
}