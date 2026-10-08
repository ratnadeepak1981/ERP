using Security.Infrastructure.Persistence.Repositories;
using Security.Interfaces;
using Security.Services;

public static class BootstrapRegistration
{
    public static IServiceCollection AddBootstrapServices(
        this IServiceCollection services)
    {
        services.AddScoped<SecurityBootstrapService>();

        services.AddScoped<
            ISecurityBootstrapRepository,
            SecurityBootstrapRepository>();

        return services;
    }
}