// Infrastructure/ServiceCollectionExtensions.cs
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using PerfectKeyV1.Application.Interfaces;
using PerfectKeyV1.Application.Services;
using PerfectKeyV1.Infrastructure.Persistence;
using PerfectKeyV1.Infrastructure.Persistence.Repositories;

namespace PerfectKeyV1.Infrastructure
{
    public static class ServiceCollectionExtensions
    {
        public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
        {
            // Đảm bảo configuration không null
            if (configuration == null)
                throw new ArgumentNullException(nameof(configuration));

            // Database
            var defaultConnection = configuration.GetConnectionString("DefaultConnection");
            
            if (string.IsNullOrWhiteSpace(defaultConnection))
            {
                Console.WriteLine("Warning: DefaultConnection is empty or null. Using fallback connection string.");
                defaultConnection = "Server=.;Database=PerfectKey;Trusted_Connection=true;TrustServerCertificate=true;";
            }
            else
            {
                // Remove whitespaces that might cause issues
                defaultConnection = defaultConnection.Trim();

                // Mask password for logging
                var masked = defaultConnection.Contains("Password=") 
                    ? System.Text.RegularExpressions.Regex.Replace(defaultConnection, "Password=[^;]+", "Password=******")
                    : defaultConnection;
                
                Console.WriteLine($"Using connection string (length: {defaultConnection.Length}): {masked}");
                
                if (defaultConnection.Length > 0)
                {
                    Console.WriteLine($"First 5 chars: [{(int)defaultConnection[0]}, {(int)defaultConnection[1]}, {(int)defaultConnection[2]}, {(int)defaultConnection[3]}, {(int)defaultConnection[4]}] (ASCII codes)");
                }
            }

            services.AddDbContext<ApplicationDbContext>(options =>
                options.UseSqlServer(defaultConnection));

            // Redis Cache với fallback
            var redisConnection = configuration.GetConnectionString("Redis");
            if (!string.IsNullOrWhiteSpace(redisConnection))
            {
                try
                {
                    services.AddStackExchangeRedisCache(options =>
                    {
                        options.Configuration = redisConnection;
                        options.InstanceName = "PerfectKey_";
                    });
                    Console.WriteLine("Redis cache registered successfully");
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Redis cache failed, using memory cache: {ex.Message}");
                    services.AddDistributedMemoryCache();
                }
            }
            else
            {
                Console.WriteLine("No Redis connection string, using memory cache");
                services.AddDistributedMemoryCache();
            }

            // HttpContext Accessor
            services.AddHttpContextAccessor();

            // REPOSITORIES
            services.AddScoped<IUserRepository, UserRepository>();
            services.AddScoped<IHotelRepository, HotelRepository>();
            services.AddScoped<IUserHotelRepository, UserHotelRepository>();
            services.AddScoped<ILoginSessionRepository, LoginSessionRepository>();
            services.AddScoped<ILayoutRepository, LayoutRepository>();
            services.AddScoped<IAreaTypeRepository, AreaTypeRepository>();
            services.AddScoped<IElementTypeRepository, PerfectKeyV1.Infrastructure.Repositories.ElementTypeRepository>();

            // SERVICES
            services.AddScoped<IUserService, UserService>();
            services.AddScoped<IHotelService, HotelService>();
            services.AddScoped<ILayoutService, LayoutService>();
            services.AddScoped<IAuthService, AuthService>();
            services.AddScoped<ILoginSessionService, LoginSessionService>();
            services.AddScoped<IJwtService, JwtService>();
            services.AddScoped<ITwoFactorService, TwoFactorService>();
            services.AddScoped<IAreaTypeService, AreaTypeService>();
            services.AddScoped<IElementTypeService, ElementTypeService>();

            return services;
        }
    }
}