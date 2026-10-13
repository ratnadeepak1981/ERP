using ERP.AI.Infrastructure.Configuration;
using ERP.AI.Infrastructure.Infrastructure.Persistence;
using ERP.AI.Infrastructure.Infrastructure.Persistence.Auditing;
using ERP.AI.Infrastructure.PythonClient;
using ERP.AI.Infrastructure.PythonClient.Interfaces;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace ERP.AI.Infrastructure.DependencyInjection;

public static class AIInfrastructureServiceCollectionExtensions
{
    public static IServiceCollection AddAIInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        // 1. Bind and register Python service configuration
        services.Configure<PythonServiceOptions>(
            configuration.GetSection(PythonServiceOptions.SectionName));

        // 2. Register typed HttpClient for Python readiness and health checks
        services.AddHttpClient<IPythonReadinessClient, PythonReadinessClient>((sp, client) =>
        {
            var options = sp.GetRequiredService<IOptions<PythonServiceOptions>>().Value;
            
            // Check ASPNETCORE_ENVIRONMENT or DOTNET_ENVIRONMENT from configuration or environment
            string env = configuration["ASPNETCORE_ENVIRONMENT"]
                ?? configuration["DOTNET_ENVIRONMENT"]
                ?? Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT")
                ?? Environment.GetEnvironmentVariable("DOTNET_ENVIRONMENT")
                ?? "Production";

            bool isDev = string.Equals(env, "Development", StringComparison.OrdinalIgnoreCase);

            // Enforce explicit validation at client initialization boundary
            options.Validate(isDev);

            string rawUrl = options.BaseUrl.Trim();
            if (!rawUrl.EndsWith("/"))
            {
                rawUrl += "/";
            }
            client.BaseAddress = new Uri(rawUrl);
            client.Timeout = TimeSpan.FromSeconds(options.TimeoutSeconds);
        });

        // 3. Register AI Audit Interceptor
        services.AddScoped<AIAuditSaveChangesInterceptor>();

        // 4. Register AIDbContext with connection string "AIDatabase"
        string? aiConnectionString = configuration.GetConnectionString("AIDatabase");
        if (!string.IsNullOrWhiteSpace(aiConnectionString))
        {
            services.AddDbContext<AIDbContext>((sp, options) =>
            {
                options.UseSqlServer(aiConnectionString);
                options.AddInterceptors(sp.GetRequiredService<AIAuditSaveChangesInterceptor>());
            });
        }
        else
        {
            // Register factory that throws clear, deliberate configuration error when AIDbContext is requested
            services.AddScoped<AIDbContext>(sp =>
            {
                throw new InvalidOperationException(
                    "AI persistence is required for this operation, but 'ConnectionStrings:AIDatabase' is not configured. " +
                    "Configure 'ConnectionStrings:AIDatabase' or environment variable 'ConnectionStrings__AIDatabase'.");
            });
        }

        return services;
    }
}
