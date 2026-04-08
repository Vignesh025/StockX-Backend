using System.IdentityModel.Tokens.Jwt;
using System.Net.Http.Headers;
using System.Security.Claims;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.IdentityModel.Tokens;
using Moq;
using StockX.Core.Entities;
using StockX.Core.Enums;
using StockX.Infrastructure.Caching;
using StockX.Infrastructure.External.AlpacaApi;
using StockX.Infrastructure.External.AlpacaApi.Models;
using StockX.Infrastructure.External.StripeApi;
using StockX.Infrastructure.Persistence.Context;

namespace StockX.Tests.IntegrationTests;

// ── No-op cache stub ──────────────────────────────────────────────────────────
// Avoids Moq open-generic-method limitations; always reports a cache miss.
internal sealed class NullCacheService : ICacheService
{
    public static readonly NullCacheService Instance = new();

    public Task<T?> GetAsync<T>(string key, CancellationToken cancellationToken = default)
        => Task.FromResult<T?>(default);

    public Task SetAsync<T>(string key, T value, TimeSpan ttl, CancellationToken cancellationToken = default)
        => Task.CompletedTask;

    public Task RemoveAsync(string key, CancellationToken cancellationToken = default)
        => Task.CompletedTask;

    public async Task<T?> GetOrCreateAsync<T>(string key, Func<Task<T>> factory, TimeSpan ttl, CancellationToken cancellationToken = default)
        => await factory();
}

/// <summary>
/// Boots the real ASP.NET Core pipeline with:
///   • EF Core In-Memory database (replaces PostgreSQL)
///   • Moq mock for IAlpacaService
///   • Moq mock for IStripeService
///   • NullCacheService (always-miss) for ICacheService
///   • Plain HTTP so HTTPS redirect middleware doesn't block tests
/// </summary>
public sealed class CustomWebApplicationFactory : WebApplicationFactory<Program>
{
    public Mock<IAlpacaService> AlpacaMock { get; } = new();
    public Mock<IStripeService> StripeMock  { get; } = new();

    // Unique DB per factory instance so test-class fixtures don't share state.
    private readonly string _dbName = Guid.NewGuid().ToString();

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");

        // Supply a dummy connection string so AddInfrastructure's guard clause
        // (which throws when the string is null/empty) passes. We overwrite the
        // real DbContext registration right after.
        builder.UseSetting("ConnectionStrings:DefaultConnection",
            "Host=localhost;Database=test_fake;Username=test;Password=test");

        // Stripe URLs referenced by PaymentService constructor
        builder.UseSetting("Stripe:SuccessUrl", "https://example.com/success");
        builder.UseSetting("Stripe:CancelUrl",  "https://example.com/cancel");

        // JWT secret — must be ≥256 bits (32 chars) for HMAC-SHA256.
        // Must match the value used in GenerateToken() below.
        builder.UseSetting("Jwt:Secret", "development-secret-key-for-tests!!");

        builder.ConfigureServices(services =>
        {
            // ── Swap real DbContext for EF Core In-Memory ─────────────────────
            // Remove ALL descriptors touching ApplicationDbContext so no Npgsql
            // option configurator survives alongside UseInMemoryDatabase.
            var toRemove = services
                .Where(d =>
                    d.ServiceType == typeof(DbContextOptions<ApplicationDbContext>) ||
                    d.ServiceType == typeof(DbContextOptions) ||
                    d.ServiceType == typeof(ApplicationDbContext) ||
                    (d.ServiceType.IsGenericType &&
                     d.ServiceType.GetGenericTypeDefinition().FullName is string n &&
                     n.Contains("IDbContextOptionsConfiguration")))
                .ToList();

            foreach (var d in toRemove)
                services.Remove(d);

            services.AddDbContext<ApplicationDbContext>(opts =>
                opts.UseInMemoryDatabase(_dbName)
                    .EnableSensitiveDataLogging());

            // ── Replace external HTTP clients with mocks ──────────────────────
            services.RemoveAll<IAlpacaService>();
            services.AddSingleton<IAlpacaService>(_ => AlpacaMock.Object);

            services.RemoveAll<IStripeService>();
            services.AddSingleton<IStripeService>(_ => StripeMock.Object);

            // ── Replace cache with always-miss stub ───────────────────────────
            services.RemoveAll<ICacheService>();
            services.AddSingleton<ICacheService>(NullCacheService.Instance);
        });
    }

    // ── Client factory helpers ────────────────────────────────────────────────

    /// <summary>Creates an unauthenticated HTTP client (no JWT, no auto-redirect).</summary>
    public new HttpClient CreateClient() =>
        CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false,
            BaseAddress       = new Uri("http://localhost")
        });

    /// <summary>Creates a client pre-wired with a Bearer JWT for <paramref name="user"/>.</summary>
    public HttpClient CreateAuthenticatedClient(User user)
    {
        var client = CreateClient();
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", GenerateToken(user));
        return client;
    }

    // ── DB seeding helpers ────────────────────────────────────────────────────

    /// <summary>
    /// Adds a <see cref="User"/> directly to the in-memory DB.
    /// The password is hashed with the same algorithm <c>AuthService</c> uses
    /// (SHA-256), so the seeded user can authenticate via /api/auth/login.
    /// </summary>
    public User SeedUser(
        string   email    = "test@example.com",
        string   name     = "Test User",
        UserRole role     = UserRole.NormalUser,
        bool     isActive = true)
    {
        using var scope = Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        var user = new User
        {
            UserId       = Guid.NewGuid(),
            Name         = name,
            Email        = email,
            PasswordHash = HashPassword("Password1!"),
            Role         = role,
            IsActive     = isActive,
            CreatedAt    = DateTime.UtcNow,
            UpdatedAt    = DateTime.UtcNow
        };

        db.Users.Add(user);
        db.SaveChanges();
        return user;
    }

    // ── JWT generation ────────────────────────────────────────────────────────

    /// <summary>
    /// Generates a signed JWT that the test host will accept.
    /// Uses the same secret as Program.cs ("development-secret-key").
    /// </summary>
    public static string GenerateToken(User user)
    {
        // Must match the Jwt:Secret injected via UseSetting above (≥32 chars = 256 bits)
        const string secret = "development-secret-key-for-tests!!";
        var key   = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(secret));
        var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

        var claims = new[]
        {
            new Claim(ClaimTypes.NameIdentifier, user.UserId.ToString()),
            new Claim(ClaimTypes.Email,          user.Email),
            new Claim(ClaimTypes.Name,           user.Name),
            new Claim(ClaimTypes.Role,           user.Role.ToString())
        };

        var token = new JwtSecurityToken(
            claims:             claims,
            expires:            DateTime.UtcNow.AddHours(1),
            signingCredentials: creds);

        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    // Mirror of AuthService.HashPassword (SHA-256, base64-encoded).
    private static string HashPassword(string password)
    {
        var bytes = Encoding.UTF8.GetBytes(password);
        var hash  = System.Security.Cryptography.SHA256.HashData(bytes);
        return Convert.ToBase64String(hash);
    }
}

// ── JSON deserialisation helper ────────────────────────────────────────────────

internal static class HttpContentExtensions
{
    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        PropertyNameCaseInsensitive = true
    };

    internal static async Task<T?> ReadAsAsync<T>(this HttpContent content)
    {
        var json = await content.ReadAsStringAsync();
        return JsonSerializer.Deserialize<T>(json, JsonOpts);
    }
}
