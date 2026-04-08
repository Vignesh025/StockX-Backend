using System.Net;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using StockX.Core.Entities;
using StockX.Core.Enums;
using StockX.Infrastructure.Persistence.Context;
using Xunit;

namespace StockX.Tests.IntegrationTests;

public sealed class AdminControllerTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly CustomWebApplicationFactory _factory;

    public AdminControllerTests(CustomWebApplicationFactory factory)
    {
        _factory = factory;
    }

    // ── GET /api/admin/users ───────────────────────────────────────────────────

    [Fact]
    public async Task GetUsers_Unauthenticated_Returns401()
    {
        var client = _factory.CreateClient();
        var response = await client.GetAsync("/api/admin/users");
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task GetUsers_NormalUser_Returns403()
    {
        var user = _factory.SeedUser(
            email: $"admin_nrm_{Guid.NewGuid()}@example.com",
            role: UserRole.NormalUser);
        var client = _factory.CreateAuthenticatedClient(user);

        var response = await client.GetAsync("/api/admin/users");

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task GetUsers_AdminUser_Returns200WithPagedResults()
    {
        var admin = _factory.SeedUser(
            email: $"admin_ok_{Guid.NewGuid()}@example.com",
            role: UserRole.Admin);
        var client = _factory.CreateAuthenticatedClient(admin);

        var response = await client.GetAsync("/api/admin/users?page=1&limit=10");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var json = await response.Content.ReadAsStringAsync();
        json.Should().Contain("items");
        json.Should().Contain("total");
        json.Should().Contain("page");
        json.Should().Contain("totalPages");
    }

    [Fact]
    public async Task GetUsers_WithSearchFilter_ReturnsFilteredResults()
    {
        var uniquePart = Guid.NewGuid().ToString("N")[..8];
        var admin = _factory.SeedUser(
            email: $"admin_srch_{uniquePart}@example.com",
            role: UserRole.Admin);
        _factory.SeedUser(email: $"target_{uniquePart}@example.com");

        var client = _factory.CreateAuthenticatedClient(admin);

        var response = await client.GetAsync($"/api/admin/users?search=target_{uniquePart}");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var json = await response.Content.ReadAsStringAsync();
        json.Should().Contain($"target_{uniquePart}");
    }

    // ── GET /api/admin/users/{userId} ──────────────────────────────────────────

    [Fact]
    public async Task GetUserDetail_Unauthenticated_Returns401()
    {
        var client = _factory.CreateClient();
        var response = await client.GetAsync($"/api/admin/users/{Guid.NewGuid()}");
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task GetUserDetail_NormalUser_Returns403()
    {
        var user = _factory.SeedUser(
            email: $"admd_nrm_{Guid.NewGuid()}@example.com",
            role: UserRole.NormalUser);
        var client = _factory.CreateAuthenticatedClient(user);

        var response = await client.GetAsync($"/api/admin/users/{Guid.NewGuid()}");
        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task GetUserDetail_AdminUser_MissingUser_Returns404()
    {
        var admin = _factory.SeedUser(
            email: $"admd_missing_{Guid.NewGuid()}@example.com",
            role: UserRole.Admin);
        var client = _factory.CreateAuthenticatedClient(admin);

        var response = await client.GetAsync($"/api/admin/users/{Guid.NewGuid()}");
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task GetUserDetail_AdminUser_ExistingUser_Returns200WithDetail()
    {
        var admin = _factory.SeedUser(
            email: $"admd_ex_adm_{Guid.NewGuid()}@example.com",
            role: UserRole.Admin);
        var target = _factory.SeedUser(
            email: $"admd_ex_tgt_{Guid.NewGuid()}@example.com");

        var client = _factory.CreateAuthenticatedClient(admin);
        var response = await client.GetAsync($"/api/admin/users/{target.UserId}");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var json = await response.Content.ReadAsStringAsync();
        json.Should().Contain(target.Email);
        json.Should().Contain("walletBalance");
        json.Should().Contain("portfolioSummary");
    }
}
