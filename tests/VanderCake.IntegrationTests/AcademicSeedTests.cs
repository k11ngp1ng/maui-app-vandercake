using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using VanderCake.Infrastructure.Identity;
using VanderCake.Infrastructure.Persistence;

namespace VanderCake.IntegrationTests;

public sealed class AcademicSeedTests
{
    [Fact]
    public async Task DEMO06_RepeatSeedPreservesCredentialsAndConsumedStockWithoutDuplicates()
    {
        var factory = new ApiFactory();
        try
        {
            await factory.SeedAsync();
            await using (var scope = factory.Services.CreateAsyncScope())
            {
                var db = scope.ServiceProvider.GetRequiredService<VanderCakeDbContext>();
                var seed = scope.ServiceProvider.GetRequiredService<DemoSeeder>();
                await seed.SeedAcademicFixtureAsync(default);
                var xyz = await db.Products.SingleAsync(p => p.Code == "XYZ");
                var lots = await db.InventoryLots.Where(l => l.ProductId == xyz.Id).ToListAsync();
                Assert.Equal(15, lots.Sum(l => l.AvailableQuantity));
                foreach (var lot in lots) lot.Consume(lot.AvailableQuantity, new DateOnly(2026, 10, 21), new DateOnly(2026, 10, 21));
                await db.SaveChangesAsync();
                var counts = new[] { await db.Users.CountAsync(), await db.Customers.CountAsync(),
                    await db.Products.CountAsync(), await db.InventoryLots.CountAsync() };
                await seed.SeedAsync(ApiFactory.Password(), ApiFactory.Password(), default);
                await seed.SeedAcademicFixtureAsync(default);
                Assert.Equal(counts, new[] { await db.Users.CountAsync(), await db.Customers.CountAsync(),
                    await db.Products.CountAsync(), await db.InventoryLots.CountAsync() });
                db.ChangeTracker.Clear();
                Assert.Equal(0, await db.InventoryLots.Where(l => l.ProductId == xyz.Id).SumAsync(l => l.AvailableQuantity));
                Assert.Equal(23500, (await db.Products.SingleAsync(p => p.Code == "EX235")).PriceMinor);
            }
            using var originalCredentials = await factory.LoginAsync();
            Assert.NotNull(originalCredentials);
        }
        finally
        {
            await factory.DisposeAsync();
            factory.DeleteDatabase();
        }
    }
}

