using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Http;
using System.Net.Http.Headers;
using Domain.Infrastructure.Persistence;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using SaaS.Infrastructure.Persistence;
using Security.Infrastructure.Persistence;
using Security.Interfaces;
using Xunit;

[assembly: CollectionBehavior(DisableTestParallelization = true)]

namespace ERP.SecurityTests;

public class TestSecurityFixture : WebApplicationFactory<API.Program>
{
    private static readonly object _initLock = new();
    private static bool _databasesReady = false;

    public TestSecurityFixture()
    {
        // Safety guard: Must end with _Test
        DatabaseSafetyGuard.AssertSafeTestDatabase(TestConstants.PlatformDbTest);
        DatabaseSafetyGuard.AssertSafeTestDatabase(TestConstants.SecurityDbTest);
        DatabaseSafetyGuard.AssertSafeTestDatabase(TestConstants.DomainDbTest);
        DatabaseSafetyGuard.AssertSafeTestDatabase(TestConstants.AIDbTest);

        // Walk up to find the repository root containing SecureVault
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir != null)
        {
            if (Directory.Exists(Path.Combine(dir.FullName, "SecureVault")))
            {
                string apiDir = Path.Combine(dir.FullName, "Backend", "ERP", "API");
                if (Directory.Exists(apiDir))
                {
                    Directory.SetCurrentDirectory(apiDir);
                }
                break;
            }
            dir = dir.Parent;
        }
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Development");

        builder.ConfigureAppConfiguration((context, config) =>
        {
            var testSettings = new Dictionary<string, string?>
            {
                ["ConnectionStrings:PlatformDatabase"] = TestConstants.PlatformDbTest,
                ["ConnectionStrings:SecurityDatabase"] = TestConstants.SecurityDbTest,
                ["ConnectionStrings:DomainDatabase"] = TestConstants.DomainDbTest,
                ["ConnectionStrings:AIDatabase"] = TestConstants.AIDbTest,
                ["Migrations:TargetPlatformMigration"] = "20261029052257_AddSpecialUomConversionSetting"
            };

            config.AddInMemoryCollection(testSettings);
        });

        builder.ConfigureServices(services =>
        {
            // Ensure AIDbContext points to AIDatabase_Test
            var descriptor = services.FirstOrDefault(d => d.ServiceType == typeof(DbContextOptions<ERP.AI.Infrastructure.Infrastructure.Persistence.AIDbContext>));
            if (descriptor != null)
            {
                services.Remove(descriptor);
            }

            services.AddDbContext<ERP.AI.Infrastructure.Infrastructure.Persistence.AIDbContext>((sp, options) =>
            {
                options.UseSqlServer(TestConstants.AIDbTest);
                options.AddInterceptors(sp.GetRequiredService<ERP.AI.Infrastructure.Infrastructure.Persistence.Auditing.AIAuditSaveChangesInterceptor>());
            });
        });
    }

    public void EnsureDatabasesInitialized()
    {
        lock (_initLock)
        {
            if (_databasesReady) return;

            using var scope = Services.CreateScope();
            var sp = scope.ServiceProvider;

            var platformDb = sp.GetRequiredService<SaaSDbContext>();
            var securityDb = sp.GetRequiredService<SecurityDbContext>();
            var domainDb = sp.GetRequiredService<DomainDbContext>();



            platformDb.Database.Migrate();
            securityDb.Database.Migrate();
            domainDb.Database.Migrate();

            TestSeeder.SeedAll(platformDb, securityDb, domainDb);

            _databasesReady = true;
        }
    }

    public HttpClient CreateAuthenticatedClient(
        Guid userId,
        string username,
        Guid? tenantId,
        IEnumerable<string> roles,
        IEnumerable<string> permissions)
    {
        EnsureDatabasesInitialized();

        using var scope = Services.CreateScope();
        var tokenService = scope.ServiceProvider.GetRequiredService<ITokenService>();

        string token = tokenService.GenerateAccessToken(
            userId,
            username,
            tenantId,
            roles,
            permissions);

        var client = CreateClient();
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", token);

        return client;
    }
}
