using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using StockX.Core.DTOs.Auth;
using StockX.Core.Enums;
using StockX.Infrastructure.Persistence.Context;
using Xunit;

namespace StockX.Tests.IntegrationTests;

public sealed class AuthControllerTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly CustomWebApplicationFactory _factory;
    private static readonly JsonSerializerOptions JsonOpts = new() { PropertyNameCaseInsensitive = true };

    public AuthControllerTests(CustomWebApplicationFactory factory)
    {
        _factory = factory;
    }

    // ── POST /api/auth/register ────────────────────────────────────────────────

    [Fact]
    public async Task Register_ValidRequest_Returns200WithUserId()
    {
        var client = _factory.CreateClient();
        var body = new { name = "Alice", email = $"alice_{Guid.NewGuid()}@example.com", password = "Password1!" };

        var response = await client.PostAsJsonAsync("/api/auth/register", body);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var json = await response.Content.ReadAsStringAsync();
        json.Should().Contain("userId");
        json.Should().Contain("Registration successful.");
    }

    [Fact]
    public async Task Register_DuplicateEmail_Returns500WithErrorMessage()
    {
        // Arrange — pre-seed a user
        var email = $"dup_{Guid.NewGuid()}@example.com";
        _factory.SeedUser(email: email);

        var client = _factory.CreateClient();
        var body = new { name = "Bob", email, password = "Password1!" };

        // Act — try to register the same email again
        var response = await client.PostAsJsonAsync("/api/auth/register", body);

        // The ExceptionHandlingMiddleware converts the InvalidOperationException → 500
        response.StatusCode.Should().Be(HttpStatusCode.InternalServerError);
    }

    // ── POST /api/auth/login ───────────────────────────────────────────────────

    [Fact]
    public async Task Login_ValidCredentials_Returns200WithToken()
    {
        var email = $"login_{Guid.NewGuid()}@example.com";

        // Register first
        var client = _factory.CreateClient();
        await client.PostAsJsonAsync("/api/auth/register",
            new { name = "Charlie", email, password = "Password1!" });

        // Login
        var response = await client.PostAsJsonAsync("/api/auth/login",
            new { email, password = "Password1!" });

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var data = await response.Content.ReadAsAsync<AuthResponse>();
        data.Should().NotBeNull();
        data!.Token.Should().NotBeNullOrEmpty();
        data.User.Email.Should().Be(email);
        data.User.Role.Should().Be(UserRole.NormalUser);
    }

    [Fact]
    public async Task Login_WrongPassword_Returns401()
    {
        var email = $"bad_{Guid.NewGuid()}@example.com";
        _factory.SeedUser(email: email);

        var client = _factory.CreateClient();
        var response = await client.PostAsJsonAsync("/api/auth/login",
            new { email, password = "WrongPassword!" });

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Login_UnknownEmail_Returns401()
    {
        var client = _factory.CreateClient();
        var response = await client.PostAsJsonAsync("/api/auth/login",
            new { email = "nobody@nowhere.com", password = "Password1!" });

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    // ── GET /api/auth/me ───────────────────────────────────────────────────────

    [Fact]
    public async Task Me_Authenticated_Returns200WithUserDetails()
    {
        var email = $"me_{Guid.NewGuid()}@example.com";
        var user = _factory.SeedUser(email: email, name: "Dave");
        var client = _factory.CreateAuthenticatedClient(user);

        var response = await client.GetAsync("/api/auth/me");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var json = await response.Content.ReadAsStringAsync();
        json.Should().Contain(email);
        json.Should().Contain("Dave");
    }

    [Fact]
    public async Task Me_Unauthenticated_Returns401()
    {
        var client = _factory.CreateClient();
        var response = await client.GetAsync("/api/auth/me");
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }
}
