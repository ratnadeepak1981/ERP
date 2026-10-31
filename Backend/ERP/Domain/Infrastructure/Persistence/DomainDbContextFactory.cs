using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Domain.Infrastructure.Persistence;

namespace Domain.Infrastructure.Persistence.Factories
{
    public class DomainDbContextFactory : IDesignTimeDbContextFactory<DomainDbContext>
    {
        public DomainDbContext CreateDbContext(string[] args)
        {
            var optionsBuilder = new DbContextOptionsBuilder<DomainDbContext>();
            // Use local SQL Server (LocalDB) for design‑time migrations
            optionsBuilder.UseSqlServer("Server=(localdb)\\mssqllocaldb;Database=DesignTimeDb;Trusted_Connection=True;");
            return new DomainDbContext(optionsBuilder.Options);
        }
    }
}
