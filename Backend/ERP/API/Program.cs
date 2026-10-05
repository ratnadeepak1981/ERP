using Domain.Services;
using Microsoft.EntityFrameworkCore;

using SaaS.Application.Interfaces;
using SaaS.Application.Services;
using SaaS.Infrastructure.Persistence;
using SaaS.Infrastructure.Persistence.Auditing;
using SaaS.Services;

using Security.Infrastructure.Persistence;
using Security.Infrastructure.Persistence.Auditing;
using Security.Interfaces;
using Security.Services;

using Domain.Infrastructure.Persistence;
using ERP.Infrastructure.Persistence.Auditing;

namespace API
{
    public class Program
    {
        public static void Main(string[] args)
        {
            var builder = WebApplication.CreateBuilder(args);
  
            // ============================================================
            // Application Services
            // ============================================================

            builder.Services.AddScoped<SaaSService>();
            builder.Services.AddScoped<DomainService>();
            builder.Services.AddScoped<SecurityService>();

            builder.Services.AddSingleton<RsaPrivateKeyLoader>();

            builder.Services.AddScoped<ITokenService, TokenService>();
            builder.Services.AddScoped<ITenantService, TenantService>();

            builder.Services.AddScoped<ISubscriptionService, SubscriptionService>();

            // ============================================================
            // Audit Interceptors
            // ============================================================

            // Domain audit
            builder.Services.AddScoped<AuditSaveChangesInterceptor>();

            // Platform / SaaS audit
            builder.Services.AddScoped<PlatformAuditSaveChangesInterceptor>();

            // Security audit
            builder.Services.AddScoped<SecurityAuditSaveChangesInterceptor>();

            // ============================================================
            // EF Core - Platform Database
            // ============================================================

            builder.Services.AddDbContext<SaaSDbContext>(
                (serviceProvider, options) =>
                {
                    options.UseSqlServer(
                        builder.Configuration.GetConnectionString(
                            "PlatformDatabase"));

                    options.AddInterceptors(
                        serviceProvider.GetRequiredService<
                            PlatformAuditSaveChangesInterceptor>());
                });

            // ============================================================
            // EF Core - Security Database
            // ============================================================

            builder.Services.AddDbContext<SecurityDbContext>(
                (serviceProvider, options) =>
                {
                    options.UseSqlServer(
                        builder.Configuration.GetConnectionString(
                            "SecurityDatabase"));

                    options.AddInterceptors(
                        serviceProvider.GetRequiredService<
                            SecurityAuditSaveChangesInterceptor>());
                });

            // ============================================================
            // EF Core - Domain Database
            // ============================================================

            builder.Services.AddDbContext<DomainDbContext>(
                (serviceProvider, options) =>
                {
                    options.UseSqlServer(
                        builder.Configuration.GetConnectionString(
                            "DomainDatabase"));

                    options.AddInterceptors(
                        serviceProvider.GetRequiredService<
                            AuditSaveChangesInterceptor>());
                });

            // ============================================================
            // Controllers / Swagger
            // ============================================================

            builder.Services.AddControllers();

            builder.Services.AddEndpointsApiExplorer();

            builder.Services.AddSwaggerGen();

            // ============================================================
            // Build Application
            // ============================================================

            var app = builder.Build();

            // ============================================================
            // HTTP Request Pipeline
            // ============================================================

            if (app.Environment.IsDevelopment())
            {
                app.UseSwagger();
                app.UseSwaggerUI();
            }

            app.UseHttpsRedirection();

            app.UseAuthorization();

            app.MapControllers();

            app.Run();
        }
    }
}