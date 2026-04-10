using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.IdentityModel.Tokens;
using StockX.API.Middleware;
using StockX.Infrastructure;
using StockX.Infrastructure.Persistence.Seeding;
using StockX.Services;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

builder.Services.AddInfrastructure(builder.Configuration);
builder.Services.AddApplicationServices();

var allowedOrigins = GetAllowedOrigins(builder.Configuration);
var useForwardedHeaders = builder.Configuration.GetValue("ForwardedHeaders:Enabled", true);
var useHttpsRedirection = builder.Configuration.GetValue("HttpsRedirection:Enabled", builder.Environment.IsDevelopment());

builder.Services.AddCors(options =>
{
    options.AddPolicy("FrontendPolicy", policy =>
    {
        policy
            .WithOrigins(allowedOrigins)
            .AllowAnyHeader()
            .AllowAnyMethod()
            .AllowCredentials();
    });
});

if (useForwardedHeaders)
{
    builder.Services.Configure<ForwardedHeadersOptions>(options =>
    {
        options.ForwardedHeaders =
            ForwardedHeaders.XForwardedFor |
            ForwardedHeaders.XForwardedProto |
            ForwardedHeaders.XForwardedHost;

        // Trust the platform proxy in containerized deployments like Render.
        options.KnownIPNetworks.Clear();
        options.KnownProxies.Clear();
    });
}

var jwtSecret = builder.Configuration["Jwt:Secret"] ??
                builder.Configuration["JWT_SECRET"] ??
                "development-secret-key";

builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = false,
            ValidateAudience = false,
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtSecret)),
            ValidateLifetime = true,
            ClockSkew = TimeSpan.FromMinutes(1)
        };
    });

var app = builder.Build();

// Seed the Stocks table in the background (non-blocking).
// If Alpaca is slow the app still starts up immediately.
_ = Task.Run(async () =>
{
    try
    {
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(30));
        await StockSeeder.SeedStocksAsync(app.Services, cts.Token);
    }
    catch (Exception ex)
    {
        var logger = app.Services.GetService<ILoggerFactory>()?.CreateLogger("StockSeeder");
        logger?.LogWarning(ex, "Background stock seeding failed — will retry next restart.");
    }
});

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

if (useForwardedHeaders)
{
    app.UseForwardedHeaders();
}

if (useHttpsRedirection && !app.Environment.IsEnvironment("Testing"))
{
    app.UseHttpsRedirection();
}

app.UseMiddleware<ExceptionHandlingMiddleware>();
app.UseMiddleware<ErrorHandlingMiddleware>();

app.UseCors("FrontendPolicy");

app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

app.Run();

static string[] GetAllowedOrigins(IConfiguration configuration)
{
    var origins = configuration
        .GetSection("AllowedOrigins")
        .Get<string[]>()
        ?.Where(origin => !string.IsNullOrWhiteSpace(origin))
        .Select(origin => origin.Trim())
        .ToArray();

    if (origins is { Length: > 0 })
    {
        return origins;
    }

    var allowedOrigins = configuration["ALLOWED_ORIGINS"];
    if (!string.IsNullOrWhiteSpace(allowedOrigins))
    {
        return allowedOrigins
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Where(origin => !string.IsNullOrWhiteSpace(origin))
            .ToArray();
    }

    return
    [
        "http://localhost:3000",
        "https://localhost:3000"
    ];
}

// Expose Program to test project for WebApplicationFactory
public partial class Program { }
