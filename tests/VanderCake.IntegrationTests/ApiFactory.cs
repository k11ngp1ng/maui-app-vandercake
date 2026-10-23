using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text.Json;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.EntityFrameworkCore;
using VanderCake.Domain.Customers;
using VanderCake.Infrastructure.Identity;
using VanderCake.Infrastructure.Persistence;

namespace VanderCake.IntegrationTests;

public sealed class ApiFactory(string? databasePath = null, Action<IServiceCollection>? configure = null) : WebApplicationFactory<Program>
{
    public string DatabasePath { get; } = databasePath ?? Path.Combine(Path.GetTempPath(), "VanderCake-tests", Guid.NewGuid() + ".db");
    public string AdminPassword { get; } = Password();
    public string SellerPassword { get; } = Password();
    public string OtherPassword { get; } = Password();
    public Guid OwnerId { get; private set; }
    public Guid OtherOwnerId { get; private set; }
    public Guid CustomerId { get; private set; }
    public Guid ProductId { get; private set; }
    public Guid GroupId { get; private set; }
    public static readonly DateTimeOffset Now = new(2026, 10, 21, 15, 0, 0, TimeSpan.Zero);
    public MutableTimeProvider Clock { get; } = new(Now);

    public static string Password() => "aA1!" + Convert.ToHexString(RandomNumberGenerator.GetBytes(20));
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Development");
        builder.UseSetting("ConnectionStrings:VanderCake", $"Data Source={DatabasePath};Pooling=False;Foreign Keys=True");
        builder.ConfigureServices(services =>
        {
            services.RemoveAll<TimeProvider>();
            services.AddSingleton<TimeProvider>(Clock);
            services.AddDataProtection().UseEphemeralDataProtectionProvider();
            configure?.Invoke(services);
        });
    }

    public async Task SeedAsync()
    {
        await using var scope = Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<VanderCakeDbContext>();
        await db.Database.MigrateAsync();
        await scope.ServiceProvider.GetRequiredService<DemoSeeder>().SeedAsync(AdminPassword, SellerPassword, CancellationToken.None);
        var users = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var other = new ApplicationUser { Id = Guid.NewGuid(), UserName = "other@vandercake.local", Email = "other@vandercake.local" };
        Assert.True((await users.CreateAsync(other, OtherPassword)).Succeeded);
        Assert.True((await users.AddToRoleAsync(other, RoleNames.Salesperson)).Succeeded);
        var profile = new Salesperson(other.Id, "Other seller");
        db.Salespersons.Add(profile); await db.SaveChangesAsync();
        OtherOwnerId = profile.Id;
        OwnerId = await db.Salespersons.Where(x => x.UserId != other.Id).Select(x => x.Id).SingleAsync();
        CustomerId = await db.Customers.Where(x => x.SalespersonId == OwnerId && x.VisitOrder == 1).Select(x => x.Id).SingleAsync();
        ProductId = await db.Products.Where(x => x.Code == "YEAST").Select(x => x.Id).SingleAsync();
        GroupId = await db.Products.Where(x => x.Id == ProductId).Select(x => x.ProductGroupId).SingleAsync();
    }

    public HttpClient Client(bool cookies = false) => CreateClient(new WebApplicationFactoryClientOptions
    {
        BaseAddress = new Uri("https://localhost"), AllowAutoRedirect = false, HandleCookies = cookies
    });

    public async Task<HttpClient> LoginAsync(string email = "salesperson@vandercake.local", string? password = null)
    {
        var client = Client();
        using var response = await client.PostAsJsonAsync("/api/auth/login", new { email,
            password = password ?? (email.StartsWith("admin") ? AdminPassword : email.StartsWith("other") ? OtherPassword : SellerPassword) });
        Assert.True(response.IsSuccessStatusCode, await response.Content.ReadAsStringAsync());
        var json = await response.Content.ReadFromJsonAsync<JsonElement>();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", json.GetProperty("accessToken").GetString());
        return client;
    }

    public void DeleteDatabase()
    {
        foreach (var suffix in new[] { "", "-wal", "-shm" })
            if (File.Exists(DatabasePath + suffix)) File.Delete(DatabasePath + suffix);
    }
    public sealed class MutableTimeProvider(DateTimeOffset initial) : TimeProvider
    {
        public DateTimeOffset Current { get; set; } = initial;
        public override DateTimeOffset GetUtcNow() => Current;
    }
}
