using API.Configuration;
using API.Security.Authorization;
using Hangfire;
using Domain.Infrastructure.Persistence;
using ERP.Infrastructure.Persistence.Auditing;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using SaaS.Application.Interfaces.Repositories;
using SaaS.Bootstrap.Subscription;
using SaaS.Infrastructure.Persistence;
using SaaS.Infrastructure.Persistence.Auditing;
using Security.Infrastructure.Persistence;
using Security.Infrastructure.Persistence.Auditing;
using Security.Infrastructure.Services;
using Security.Services;
using System.Security.Claims;

namespace API;

public class Program
{
    public static async Task Main(string[] args)
    {
        var builder = WebApplication.CreateBuilder(args);

        // DI
        builder.Services.AddApplicationServices();
        builder.Services.AddApplicationRepositories();
        builder.Services.AddBootstrapServices();

        // Audit
        builder.Services.AddScoped<AuditSaveChangesInterceptor>();
        builder.Services.AddScoped<PlatformAuditSaveChangesInterceptor>();
        builder.Services.AddScoped<SecurityAuditSaveChangesInterceptor>();

        // Databases
        builder.Services.AddDbContext<SaaSDbContext>(
            (sp, options) =>
            {
                options.UseSqlServer(
                    builder.Configuration.GetConnectionString(
                        "PlatformDatabase"));

                options.AddInterceptors(
                    sp.GetRequiredService<
                        PlatformAuditSaveChangesInterceptor>());
            });

        // Hangfire Persistent SQL Server Storage in Platform database ([HangFire] schema)
        string? platformConnStr = builder.Configuration.GetConnectionString("PlatformDatabase");
        if (!string.IsNullOrEmpty(platformConnStr))
        {
            builder.Services.AddHangfire(config => config
                .SetDataCompatibilityLevel(Hangfire.CompatibilityLevel.Version_180)
                .UseSimpleAssemblyNameTypeSerializer()
                .UseRecommendedSerializerSettings()
                .UseSqlServerStorage(platformConnStr, new Hangfire.SqlServer.SqlServerStorageOptions
                {
                    CommandBatchMaxTimeout = TimeSpan.FromMinutes(5),
                    SlidingInvisibilityTimeout = TimeSpan.FromMinutes(5),
                    QueuePollInterval = TimeSpan.Zero,
                    UseRecommendedIsolationLevel = true,
                    DisableGlobalLocks = true,
                    SchemaName = "HangFire"
                }));

            builder.Services.AddHangfireServer(options =>
            {
                options.WorkerCount = Environment.ProcessorCount * 2;
            });
        }

        builder.Services.AddDbContext<SecurityDbContext>(
            (sp, options) =>
            {
                options.UseSqlServer(
                    builder.Configuration.GetConnectionString(
                        "SecurityDatabase"));

                // TEMPORARY EF DIAGNOSTIC LOGGING
                options.EnableSensitiveDataLogging();
                options.LogTo(Console.WriteLine);

                options.AddInterceptors(
                    sp.GetRequiredService<
                        SecurityAuditSaveChangesInterceptor>());
            });

        builder.Services.AddDbContext<DomainDbContext>(
            (sp, options) =>
            {
                options.UseSqlServer(
                    builder.Configuration.GetConnectionString(
                        "DomainDatabase"));

                options.AddInterceptors(
                    sp.GetRequiredService<
                        AuditSaveChangesInterceptor>());
            });

        // Authentication
        builder.Services
            .AddAuthentication(
                JwtBearerDefaults.AuthenticationScheme)
            .AddJwtBearer(options =>
            {
                var keyLoader = new RsaPrivateKeyLoader();

                if (!keyLoader.TryLoadPublicKey())
                {
                    throw new InvalidOperationException(
                        "RSA public key could not be loaded.");
                }

                var rsa = keyLoader.GetPublicRsa()
                    ?? throw new InvalidOperationException(
                        "RSA public key is unavailable.");

                options.TokenValidationParameters =
                    new TokenValidationParameters
                    {
                        ValidateIssuerSigningKey = true,
                        IssuerSigningKey = new RsaSecurityKey(rsa),

                        ValidateIssuer = true,
                        ValidIssuer = "CSharpAuthServer",

                        ValidateAudience = true,
                        ValidAudience = "ERP",

                        ValidateLifetime = true,
                        ClockSkew = TimeSpan.FromMinutes(1),

                        RoleClaimType = ClaimTypes.Role
                    };
            });

        // Dynamic Permission Authorization
        builder.Services.AddSingleton<
            IAuthorizationPolicyProvider,
            PermissionPolicyProvider>();

        builder.Services.AddScoped<
            IAuthorizationHandler,
            PermissionAuthorizationHandler>();

        builder.Services.AddAuthorization();

        // Controllers / Swagger
        builder.Services.AddControllers();
        builder.Services.AddEndpointsApiExplorer();

        builder.Services.AddSwaggerGen(options =>
        {
            options.AddSecurityDefinition(
                "Bearer",
                new Microsoft.OpenApi.Models.OpenApiSecurityScheme
                {
                    Name = "Authorization",
                    Type = Microsoft.OpenApi.Models.SecuritySchemeType.Http,
                    Scheme = "bearer",
                    BearerFormat = "JWT",
                    In = Microsoft.OpenApi.Models.ParameterLocation.Header,
                    Description = "Enter your JWT token."
                });

            options.AddSecurityRequirement(
                new Microsoft.OpenApi.Models.OpenApiSecurityRequirement
                {
                    {
                        new Microsoft.OpenApi.Models.OpenApiSecurityScheme
                        {
                            Reference =
                                new Microsoft.OpenApi.Models.OpenApiReference
                                {
                                    Type =
                                        Microsoft.OpenApi.Models.ReferenceType.SecurityScheme,
                                    Id = "Bearer"
                                }
                        },
                        Array.Empty<string>()
                    }
                });
        });

        var app = builder.Build();

        // Database Migration and Seed
        using (var scope = app.Services.CreateScope())
        {
            var services = scope.ServiceProvider;

            // SaaS / Platform DB
            var platformDb =
                services.GetRequiredService<SaaSDbContext>();

            string? targetPlatformMigration = builder.Configuration["Migrations:TargetPlatformMigration"];
            if (!string.IsNullOrEmpty(targetPlatformMigration))
            {
                var migrator = Microsoft.EntityFrameworkCore.Infrastructure.AccessorExtensions.GetInfrastructure(platformDb)
                    .GetService<Microsoft.EntityFrameworkCore.Migrations.IMigrator>();
                migrator?.Migrate(targetPlatformMigration);
            }
            else
            {
                await platformDb.Database.MigrateAsync();
            }

            SubscriptionSeed.Seed(
                services.GetRequiredService<ISubscriptionPlanRepository>(),
                services.GetRequiredService<ISubscriptionParameterRepository>(),
                services.GetRequiredService<ISubscriptionLimitRepository>(),
                services.GetRequiredService<ISubscriptionPlanParameterRepository>());

            // Security DB
            var securityDb =
                services.GetRequiredService<SecurityDbContext>();

            await securityDb.Database.MigrateAsync();

            var securityBootstrap =
                services.GetRequiredService<SecurityBootstrapService>();

            await securityBootstrap.EnsurePlatformRbacAsync();

            await securityBootstrap.EnsureDomainPermissionsAsync();

            // Domain DB
            var domainDb =
                services.GetRequiredService<DomainDbContext>();

            await domainDb.Database.MigrateAsync();
        }

        // HTTP Pipeline
        if (app.Environment.IsDevelopment())
        {
            app.UseSwagger();
            app.UseSwaggerUI();
        }

        app.UseHttpsRedirection();

        app.UseAuthentication();
        app.UseAuthorization();

        // Hangfire Dashboard (Secured with PlatformAdmin / SecurityManager role authorization)
        app.MapHangfireDashboard("/hangfire", new Hangfire.DashboardOptions
        {
            Authorization = new[] { new HangfireAuthorizationFilter() }
        });

        using (var scope = app.Services.CreateScope())
        {
            var recurringJobManager = scope.ServiceProvider.GetService<IRecurringJobManager>();
            if (recurringJobManager != null)
            {
                RecurringBillingJobsConfig.ScheduleRecurringJobs(recurringJobManager);
            }
        }

        app.MapControllers();

        app.Run();
    }
}