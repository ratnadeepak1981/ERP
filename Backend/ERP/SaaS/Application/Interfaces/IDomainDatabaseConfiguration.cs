namespace SaaS.Application.Interfaces;

public interface IDomainDatabaseConfiguration
{
    string DatabaseName { get; }

    string DatabaseServer { get; }
}