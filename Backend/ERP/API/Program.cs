using API.Security;
using API.Services;
using Domain.Features.MasterData.Company;
using Domain.Features.MasterData.Product;
using Domain.Infrastructure.Persistence;
using Domain.Services;
using ERP.Infrastructure.Persistence.Auditing;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using SaaS.Application.Interfaces;
using SaaS.Application.Interfaces.Repositories;
using SaaS.Application.Services;
using SaaS.Infrastructure.Persistence;
using SaaS.Infrastructure.Persistence.Auditing;
using SaaS.Infrastructure.Persistence.Repositories;
using SaaS.Infrastructure.Repositories;
using SaaS.Services;
using Security.Infrastructure.Persistence;
using Security.Infrastructure.Persistence.Auditing;
using Security.Infrastructure.Persistence.Repositories;
using Security.Infrastructure.Services;
using Security.Interfaces;
using Security.Services;
using System.Security.Claims;

namespace API
{
    public class Program
    {
        public static async Task Main(string[] args)
        {
            var builder = WebApplication.CreateBuilder(args);


            builder.Services.AddScoped<IDomainDatabaseConfiguration, DomainDatabaseConfiguration>();


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

            builder.Services.AddScoped<IRolePermissionService,RolePermissionService>();

            builder.Services.AddScoped<IRoleService, RoleService>();

            builder.Services.AddScoped<IPermissionService,PermissionService>();

            builder.Services.AddScoped<IUserService,UserService>();

            builder.Services.AddScoped<IUserRoleService,UserRoleService>();


            builder.Services.AddScoped<SecurityBootstrapService>();
            builder.Services.AddScoped<PasswordService>();

            builder.Services.AddScoped<ISubscriptionPlanService,SubscriptionPlanService>();

            builder.Services.AddScoped<ISubscriptionParameterService,SubscriptionParameterService>();

            builder.Services.AddScoped<ISubscriptionPlanParameterService,SubscriptionPlanParameterService>();

            builder.Services.AddScoped<ISubscriptionLimitService,SubscriptionLimitService>();

            builder.Services.AddScoped<ITenantProvisioningService,TenantProvisioningService>();

            builder.Services.AddScoped<ITenantAdminProvisioningService,TenantAdminProvisioningService>();

            builder.Services.AddScoped<ISubscriptionUsageRepository,SubscriptionUsageRepository>();

            builder.Services.AddScoped<ISubscriptionUsageService,SubscriptionUsageService>();

            builder.Services.AddScoped<ICompanyService, CompanyService>();

            builder.Services.AddScoped<IUserScopeService, UserScopeService>();

            builder.Services.AddScoped<IUserAccessService, UserAccessService>();

            builder.Services.AddScoped<IProductService, ProductService>();
            

            // ============================================================
            // Current User Context
            // ============================================================

            builder.Services.AddHttpContextAccessor();

            builder.Services.AddScoped<ICurrentUserContext,CurrentUserContext>();

            // ============================================================
            // Role Repository
            // ============================================================

            builder.Services.AddScoped<IRoleRepository,RoleRepository>();

            builder.Services.AddScoped<IPermissionRepository,PermissionRepository>();

            builder.Services.AddScoped<IRolePermissionRepository,RolePermissionRepository>();

            builder.Services.AddScoped<IUserRepository,UserRepository>();

            builder.Services.AddScoped<IUserRoleRepository,UserRoleRepository>();

            builder.Services.AddScoped<ITenantDatabaseRepository,TenantDatabaseRepository>();

            builder.Services.AddScoped<ITenantConfigurationRepository,TenantConfigurationRepository>();

            // ============================================================
            // Security Bootstrap Services
            // ============================================================

            builder.Services.AddScoped<ISecurityBootstrapRepository,SecurityBootstrapRepository>();

            builder.Services.AddScoped<ITenantRepository,TenantRepository>();

            builder.Services.AddScoped<ISubscriptionParameterRepository,SubscriptionParameterRepository>();

            builder.Services.AddScoped<ISubscriptionPlanParameterRepository,SubscriptionPlanParameterRepository>();

            builder.Services.AddScoped<ISubscriptionLimitRepository,SubscriptionLimitRepository>();

            builder.Services.AddScoped<ISubscriptionRepository,SubscriptionRepository>();

            builder.Services.AddScoped<ICompanyRepository, CompanyRepository>();

            builder.Services.AddScoped<IProductRepository, ProductRepository>();

            // ============================================================
            // Audit Interceptors
            // ============================================================

            builder.Services.AddScoped<AuditSaveChangesInterceptor>();
            builder.Services.AddScoped<PlatformAuditSaveChangesInterceptor>();
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
            // JWT Authentication
            // ============================================================

            builder.Services.AddAuthentication(
                JwtBearerDefaults.AuthenticationScheme)
                .AddJwtBearer(options =>
                {
                    var keyLoader =
                        new RsaPrivateKeyLoader();

                    if (!keyLoader.TryLoadPublicKey())
                    {
                        throw new InvalidOperationException(
                            "RSA public key could not be loaded.");
                    }

                    var rsa =
                        keyLoader.GetPublicRsa()
                        ?? throw new InvalidOperationException(
                            "RSA public key is unavailable.");

                    options.TokenValidationParameters =
                        new TokenValidationParameters
                        {
                            ValidateIssuerSigningKey = true,

                            IssuerSigningKey =
                                new RsaSecurityKey(rsa),

                            ValidateIssuer = true,

                            ValidIssuer =
                                "CSharpAuthServer",

                            ValidateAudience = true,

                            ValidAudience =
                                "ERP",

                            ValidateLifetime = true,

                            ClockSkew =
                                TimeSpan.FromMinutes(1),

                            RoleClaimType =
                                ClaimTypes.Role
                        };
                });

            builder.Services.AddAuthorization(options =>
            {
                options.AddPolicy(
                    "SUBSCRIPTION_VIEW",
                    policy =>
                    {
                        policy.RequireAuthenticatedUser();

                        policy.RequireClaim(
                            "permission",
                            "SUBSCRIPTION_VIEW");
                    });

                options.AddPolicy(
                    "USER_VIEW",
                    policy =>
                    {
                        policy.RequireAuthenticatedUser();

                        policy.RequireClaim(
                            "permission",
                            "USER_VIEW");

                    });
                options.AddPolicy("PRODUCT_VIEW", policy =>
                        policy.RequireClaim("permission", "PRODUCT_VIEW"));

                options.AddPolicy("COMPANY_VIEW", policy =>
                    policy.RequireClaim("permission", "COMPANY_VIEW"));
            });
            // ============================================================
            // Controllers / Swagger
            // ============================================================

            builder.Services.AddControllers();

            builder.Services.AddEndpointsApiExplorer();

            builder.Services.AddSwaggerGen(options =>
            {
                options.AddSecurityDefinition(
                    "Bearer",
                    new Microsoft.OpenApi.Models.OpenApiSecurityScheme
                    {
                        Name = "Authorization",

                        Type =
                            Microsoft.OpenApi.Models.SecuritySchemeType.Http,

                        Scheme = "bearer",

                        BearerFormat = "JWT",

                        In =
                            Microsoft.OpenApi.Models.ParameterLocation.Header,

                        Description =
                            "Enter your JWT token."
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

            // ============================================================
            // Build Application
            // ============================================================

            var app = builder.Build();

            // ============================================================
            // Database Migration + Platform Admin Bootstrap
            // ============================================================

            using (var scope = app.Services.CreateScope())
            {
                var services = scope.ServiceProvider;

                var platformDb =
                    services.GetRequiredService<SaaSDbContext>();

                await platformDb.Database.MigrateAsync();

                var securityDb =
                    services.GetRequiredService<SecurityDbContext>();

                await securityDb.Database.MigrateAsync();

                var domainDb =
                    services.GetRequiredService<DomainDbContext>();

                await domainDb.Database.MigrateAsync();

                var bootstrap =
                    services.GetRequiredService<
                        SecurityBootstrapService>();

                await bootstrap.EnsurePlatformAdminAsync();
            }

            // ============================================================
            // HTTP Request Pipeline
            // ============================================================

            if (app.Environment.IsDevelopment())
            {
                app.UseSwagger();
                app.UseSwaggerUI();
            }

            app.UseHttpsRedirection();

            app.UseAuthentication();

            app.UseAuthorization();

            app.MapControllers();

            app.Run();
        }
    }
}
