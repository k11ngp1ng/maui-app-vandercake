using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using VanderCake.Contracts.Health;
using VanderCake.Infrastructure.Persistence;

namespace VanderCake.IntegrationTests;

public sealed class FoundationFactory : WebApplicationFactory<Program>
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Development");
        builder.UseSetting("ConnectionStrings:VanderCake", "Data Source=:memory:");
    }
}

public sealed class FoundationTests(FoundationFactory factory) : IClassFixture<FoundationFactory>
{
    [Fact]
    public async Task FND01_HealthReturnsPublicJsonContract()
    {
        using var client = factory.CreateClient();
        using var response = await client.GetAsync("/health");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("application/json", response.Content.Headers.ContentType?.MediaType);
        var body = await response.Content.ReadFromJsonAsync<HealthResponse>();
        Assert.Equal(new HealthResponse("healthy", "VanderCake"), body);
    }

    [Fact]
    public async Task FND02_RazorPagesRunInTheSameHost()
    {
        using var client = factory.CreateClient();
        using var response = await client.GetAsync("/");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("text/html", response.Content.Headers.ContentType?.MediaType);
        Assert.Contains("VanderCake", await response.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task FND03_ServerResolvesWorkingSqliteProvider()
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var database = scope.ServiceProvider.GetRequiredService<VanderCakeDbContext>();
        Assert.Equal("Microsoft.EntityFrameworkCore.Sqlite", database.Database.ProviderName);
        await database.Database.OpenConnectionAsync();
        await using var command = database.Database.GetDbConnection().CreateCommand();
        command.CommandText = "SELECT 1";
        Assert.Equal(1L, await command.ExecuteScalarAsync());
    }
}

