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
                Console.WriteLine("Warning: DefaultConnection is empty or null. Using fallback.");
                defaultConnection = "Server=localhost;Database=PerfectKey;User Id=sa;Password=Password123;Encrypt=false;";
            }
            else
            {
                // Aggressively clean the connection string
                string original = defaultConnection;
                
                // Remove any surrounding quotes (sometimes added by env var managers)
                defaultConnection = defaultConnection.Trim().Trim('"').Trim('\'');
                
                // Remove non-printable characters or BOMs
                defaultConnection = new string(defaultConnection.Where(c => c >= 32 && c <= 126).ToArray()).Trim();

                // Mask password for logging
                var masked = defaultConnection.Contains("Password=") 
                    ? System.Text.RegularExpressions.Regex.Replace(defaultConnection, "Password=[^;]+", "Password=******")
                    : (defaultConnection.Contains("PWD=") 
                        ? System.Text.RegularExpressions.Regex.Replace(defaultConnection, "PWD=[^;]+", "PWD=******")
                        : defaultConnection);
                
                Console.WriteLine($"ConnString cleaned. Original length: {original.Length}, New length: {defaultConnection.Length}");
                Console.WriteLine($"Using: {masked}");
                
                if (defaultConnection.Length > 0)
                {
                    Console.WriteLine($"Index 0 char code: {(int)defaultConnection[0]} (char: '{defaultConnection[0]}')");
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