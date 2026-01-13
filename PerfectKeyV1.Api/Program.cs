using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using PerfectKeyV1.Api.Filters;
using PerfectKeyV1.Infrastructure;
using System.Reflection;
using System.Security.Claims;
using System.IdentityModel.Tokens.Jwt;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

var builder = WebApplication.CreateBuilder(args);

// ==================== CONFIGURATION ====================
builder.Configuration
    .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true)
    .AddJsonFile($"appsettings.{builder.Environment.EnvironmentName}.json", optional: true)
    .AddEnvironmentVariables();

// ==================== PORT (RENDER) ====================
var port = Environment.GetEnvironmentVariable("PORT") ?? "10000";
builder.WebHost.UseUrls($"http://0.0.0.0:{port}");

// ==================== SERVICES ====================
builder.Services.AddControllers()
    .AddJsonOptions(options =>
    {
        options.JsonSerializerOptions.ReferenceHandler = ReferenceHandler.IgnoreCycles;
        options.JsonSerializerOptions.DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull;
        options.JsonSerializerOptions.PropertyNamingPolicy = JsonNamingPolicy.CamelCase;
        options.JsonSerializerOptions.WriteIndented = true;
    });

// ==================== CORS ====================
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowAll", policy =>
    {
        policy
            .AllowAnyOrigin()
            .AllowAnyMethod()
            .AllowAnyHeader()
            .WithExposedHeaders("X-Total-Count", "Content-Disposition");
    });
});

// ==================== AUTHENTICATION (JWT GATEWAY) ====================
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
.AddJwtBearer(options =>
{
    options.TokenHandlers.Clear();
    options.TokenHandlers.Add(new NoSignatureValidationJwtHandler());

    options.TokenValidationParameters = new TokenValidationParameters
    {
        ValidateIssuer = false,
        ValidateAudience = false,
        ValidateLifetime = true,
        ValidateIssuerSigningKey = false,
        IssuerSigningKeyResolver = (_, _, _, _) =>
            new List<SecurityKey> {
                new SymmetricSecurityKey(Encoding.UTF8.GetBytes("dummy-key"))
            },
        ClockSkew = TimeSpan.Zero
    };

    options.Events = new JwtBearerEvents
    {
        OnTokenValidated = async context =>
        {
            var userIdClaim = context.Principal?.FindFirst("userId");
            if (userIdClaim != null && int.TryParse(userIdClaim.Value, out var userId))
            {
                var userRepo = context.HttpContext.RequestServices
                    .GetRequiredService<PerfectKeyV1.Application.Interfaces.IUserRepository>();

                var user = await userRepo.GetByIdAsync(userId);
                if (user != null && context.Principal?.Identity is ClaimsIdentity identity)
                {
                    identity.AddClaim(new Claim(ClaimTypes.NameIdentifier, user.Id.ToString()));
                    identity.AddClaim(new Claim(ClaimTypes.Name, user.UserName ?? ""));
                    identity.AddClaim(new Claim(ClaimTypes.Email, user.Email ?? ""));
                    identity.AddClaim(new Claim("userType",
                        user.UserType.HasValue ? ((int)user.UserType.Value).ToString() : "1"));
                }
            }
        }
    };
});

// ==================== AUTHORIZATION ====================
builder.Services.AddAuthorization(options =>
{
    options.AddPolicy("AdminOnly", policy =>
        policy.RequireClaim("userType", "0", "2"));

    options.AddPolicy("SuperAdminOnly", policy =>
        policy.RequireClaim("userType", "2"));

    options.AddPolicy("StaffOrAdmin", policy =>
        policy.RequireClaim("userType", "0", "1", "2"));
});

// ==================== INFRASTRUCTURE ====================
builder.Services.AddInfrastructure(builder.Configuration);

// ==================== HTTP CLIENT ====================
builder.Services.AddHttpClient();
builder.Services.AddHttpClient("Gateway", client =>
{
    client.BaseAddress = new Uri(
        builder.Configuration["Gateway:BaseUrl"] ?? "https://sit.api-pms.perfectkey.vn"
    );
});

// ==================== SWAGGER ====================
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc("v1", new OpenApiInfo
    {
        Title = "PerfectKey API",
        Version = "v1"
    });

    options.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Name = "Authorization",
        Type = SecuritySchemeType.Http,
        Scheme = "Bearer",
        BearerFormat = "JWT",
        In = ParameterLocation.Header
    });

    options.AddSecurityRequirement(new OpenApiSecurityRequirement
    {
        {
            new OpenApiSecurityScheme
            {
                Reference = new OpenApiReference
                {
                    Type = ReferenceType.SecurityScheme,
                    Id = "Bearer"
                }
            },
            Array.Empty<string>()
        }
    });

    options.OperationFilter<AddAuthHeaderOperationFilter>();

    // Fix for duplicate schemaId "UserDto"
    options.CustomSchemaIds(type => type.ToString());

    var xmlFile = $"{Assembly.GetExecutingAssembly().GetName().Name}.xml";
    var xmlPath = Path.Combine(AppContext.BaseDirectory, xmlFile);
    Console.WriteLine($"Looking for Swagger XML at: {xmlPath}");
    if (File.Exists(xmlPath))
    {
        options.IncludeXmlComments(xmlPath);
        Console.WriteLine("Swagger XML comments included.");
    }
    else
    {
        Console.WriteLine("Warning: Swagger XML file not found.");
    }
});

// ==================== LOGGING ====================
builder.Services.AddLogging(logging =>
{
    logging.AddConsole();
    logging.SetMinimumLevel(LogLevel.Information);
});

// ==================== HEALTH CHECK ====================
builder.Services.AddHealthChecks();

// ==================== BUILD ====================
var app = builder.Build();

app.UseDeveloperExceptionPage();

app.UseSwagger(c =>
{
    c.RouteTemplate = "swagger/{documentName}/swagger.json";
});

app.UseSwaggerUI(c =>
{
    c.SwaggerEndpoint("/swagger/v1/swagger.json", "PerfectKey API v1");
    c.RoutePrefix = "swagger";
    c.DisplayRequestDuration();
    c.DefaultModelsExpandDepth(-1); // Hide schemas by default
});

app.UseCors("AllowAll");

// Render is always Production, but we want to see errors and use Https
if (app.Environment.IsDevelopment())
{
    app.UseHttpsRedirection();
}
else
{
    // On Render, HTTPS is handled by the load balancer, but we can still redirect if needed
    // app.UseHttpsRedirection(); 
}

app.UseAuthentication();
app.UseAuthorization();

app.MapHealthChecks("/health");
app.MapControllers();

// ==================== START ====================
Console.WriteLine("====================================");
Console.WriteLine("🚀 PerfectKey Backend Started");
Console.WriteLine($"🌍 Environment: {app.Environment.EnvironmentName}");
Console.WriteLine($"🔌 Port: {port}");
Console.WriteLine("====================================");

app.Run();


// ==================== CUSTOM JWT HANDLER ====================
public class NoSignatureValidationJwtHandler : JwtSecurityTokenHandler
{
    public override ClaimsPrincipal ValidateToken(
        string token,
        TokenValidationParameters validationParameters,
        out SecurityToken validatedToken)
    {
        validationParameters.ValidateIssuerSigningKey = false;
        validationParameters.SignatureValidator = (t, p) => new JwtSecurityToken(t);
        return base.ValidateToken(token, validationParameters, out validatedToken);
    }
}
