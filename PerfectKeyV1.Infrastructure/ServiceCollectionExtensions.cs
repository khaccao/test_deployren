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

            // Database - Check multiple possible keys for the connection string
            var defaultConnection = configuration.GetConnectionString("DefaultConnection")
                ?? configuration["DefaultConnection"]
                ?? configuration["ConnectionStrings:DefaultConnection"]
                ?? configuration["DB_CONNECTION"];

            if (string.IsNullOrWhiteSpace(defaultConnection))
            {
                Console.WriteLine("Warning: DefaultConnection is empty or null. Using fallback.");
                defaultConnection = "Server=localhost;Database=PerfectKey;User Id=sa;Password=Password123;Encrypt=false;";
            }
            else
            {
                string original = defaultConnection;
                
                // 1. Remove literal quotes if they exist
                if (defaultConnection.StartsWith("\"") && defaultConnection.EndsWith("\""))
                    defaultConnection = defaultConnection.Substring(1, defaultConnection.Length - 2);
                if (defaultConnection.StartsWith("'") && defaultConnection.EndsWith("'"))
                    defaultConnection = defaultConnection.Substring(1, defaultConnection.Length - 2);

                // 2. Trim and check for BOM or hidden characters
                defaultConnection = defaultConnection.Trim();
                
                // 3. Log details for debugging
                var masked = defaultConnection.Contains("Password=") 
                    ? System.Text.RegularExpressions.Regex.Replace(defaultConnection, "Password=[^;]+", "Password=******")
                    : (defaultConnection.Contains("PWD=") 
                        ? System.Text.RegularExpressions.Regex.Replace(defaultConnection, "PWD=[^;]+", "PWD=******")
                        : "NOT_MASKED_OR_HIDDEN");

                Console.WriteLine("--- DB CONFIGURATION DEBUG ---");
                Console.WriteLine($"Original Length: {original.Length}");
                Console.WriteLine($"Final Length: {defaultConnection.Length}");
                
                if (defaultConnection.Length > 0)
                {
                    var firstBytes = string.Join(", ", defaultConnection.Take(Math.Min(10, defaultConnection.Length)).Select(c => ((int)c).ToString()));
                    Console.WriteLine($"First {Math.Min(10, defaultConnection.Length)} char codes: [{firstBytes}]");
                }
                
                if (masked == "NOT_MASKED_OR_HIDDEN") 
                {
                    // If not masked, show a safe version
                    var safeShow = defaultConnection.Length > 20 ? defaultConnection.Substring(0, 20) + "..." : defaultConnection;
                    Console.WriteLine($"ConnString (safe start): {safeShow}");
                }
                else 
                {
                    Console.WriteLine($"ConnString (masked): {masked}");
                }
                Console.WriteLine("------------------------------");

                if (string.IsNullOrWhiteSpace(defaultConnection))
                {
                    Console.WriteLine("CRITICAL: Connection string became empty after cleaning! Reverting to fallback.");
                    defaultConnection = "Server=localhost;Database=PerfectKey;User Id=sa;Password=Password123;Encrypt=false;";
                }
            }

            // Test if we can create a SqlConnectionStringBuilder (will throw if format is bad)
            try 
            {
                var builder = new Microsoft.Data.SqlClient.SqlConnectionStringBuilder(defaultConnection);
                Console.WriteLine("✓ Connection string format validated by SqlConnectionStringBuilder");
            }
            catch (Exception ex) 
            {
                Console.WriteLine($"⚠ Connection string format validation failed: {ex.Message}");
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