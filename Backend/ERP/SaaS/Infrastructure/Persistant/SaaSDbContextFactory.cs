using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using SaaS.Infrastructure.Persistence;

namespace SaaS.Infrastructure.Persistence.Factories;

public class SaaSDbContextFactory : IDesignTimeDbContextFactory<SaaSDbContext>
{
    public SaaSDbContext CreateDbContext(string[] args)
    {
        var optionsBuilder = new DbContextOptionsBuilder<SaaSDbContext>();
        optionsBuilder.UseSqlServer("Server=(localdb)\\mssqllocaldb;Database=PlatformDB;Trusted_Connection=True;TrustServerCertificate=True;");
        return new SaaSDbContext(optionsBuilder.Options);
    }
}
