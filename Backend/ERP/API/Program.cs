using Domain.Services;
using Microsoft.EntityFrameworkCore;
using SaaS.Application.Interfaces;
using SaaS.Application.Services;
using SaaS.Infrastructure.Persistence;
using SaaS.Services;
using Security.Interfaces;
using Security.Services;

namespace API
{
    public class Program
    {
        public static void Main(string[] args)
        {
            var builder = WebApplication.CreateBuilder(args);

            // Add services to the container.
            builder.Services.AddScoped<SaaSService>();
            builder.Services.AddScoped<DomainService>();
            builder.Services.AddScoped<SecurityService>();
            builder.Services.AddSingleton<RsaPrivateKeyLoader>();

            builder.Services.AddScoped<ITokenService, TokenService>();
            builder.Services.AddScoped<ITenantService, TenantService>();

            builder.Services.AddScoped<ISubscriptionService, SubscriptionService>();

            // EF Core - Platform Database
            builder.Services.AddDbContext<SaaSDbContext>(options =>  options.UseSqlServer( builder.Configuration.GetConnectionString("PlatformDatabase")));

            builder.Services.AddControllers();

            builder.Services.AddEndpointsApiExplorer();
            builder.Services.AddSwaggerGen();

            var app = builder.Build();

            // Configure the HTTP request pipeline.
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