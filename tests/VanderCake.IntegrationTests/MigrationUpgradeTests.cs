using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.Extensions.DependencyInjection;
using VanderCake.Contracts.Api;
using VanderCake.Infrastructure.Identity;
using VanderCake.Infrastructure.Persistence;

namespace VanderCake.IntegrationTests;

public sealed class MigrationUpgradeTests
{
    [Fact]
    public async Task DB06_UpgradePreservesPhase02OrderItemsAndInventory()
    {
        await using var factory = new ApiFactory();
        try
        {
            Guid orderId = Guid.NewGuid();
            Guid customerId;
            await using (var scope = factory.Services.CreateAsyncScope())
            {
                var db = scope.ServiceProvider.GetRequiredService<VanderCakeDbContext>();
                var migrator = db.GetService<IMigrator>();
                var initial = db.Database.GetMigrations().First();
                await migrator.MigrateAsync(initial);
                await scope.ServiceProvider.GetRequiredService<DemoSeeder>()
                    .SeedAsync(factory.AdminPassword, factory.SellerPassword, CancellationToken.None);
                var customer = await db.Customers.OrderBy(c => c.VisitOrder).FirstAsync();
                customerId = customer.Id;
                var product = await db.Products.SingleAsync(p => p.Code == "YEAST");
                var id = orderId; var version = Guid.NewGuid(); var key = Guid.NewGuid();
                var submitted = ApiFactory.Now.UtcDateTime;
                var date = new DateOnly(2026, 10, 22);
                // Insert only the columns that existed in Phase 02, before AllocationBatchId.
                await db.Database.ExecuteSqlInterpolatedAsync($"""
                    INSERT INTO Orders (Id, Version, CustomerId, SalespersonId, CustomerName, SalespersonName,
                        PaymentTermCode, DispatchDate, DispatchWindow, SubmittedAtUtc, SubmissionKey, RequestFingerprint, Status)
                    VALUES ({id}, {version}, {customer.Id}, {customer.SalespersonId}, {customer.Name}, {"Demo Salesperson"},
                        {10}, {date}, {19}, {submitted}, {key}, {"legacy"}, {0})
                    """);
                await db.Database.ExecuteSqlInterpolatedAsync($"""
                    INSERT INTO OrderItems (Id, Version, OrderId, ProductId, ProductName, RequestedQuantity, FulfilledQuantity, UnitPriceMinor)
                    VALUES ({Guid.NewGuid()}, {Guid.NewGuid()}, {id}, {product.Id}, {product.Name}, {2}, {0}, {1250})
                    """);
                db.ChangeTracker.Clear();
                await migrator.MigrateAsync();
                Assert.False(db.Database.HasPendingModelChanges());
                Assert.Equal(18, await db.InventoryLots.Where(l => l.ProductId == product.Id).SumAsync(l => l.AvailableQuantity));
            }
            using var seller = await factory.LoginAsync();
            var restored = (await seller.GetFromJsonAsync<OrderResponse>($"/api/orders/{orderId}"))!;
            Assert.Equal(customerId, restored.CustomerId);
            Assert.Equal(2500, restored.RequestedTotalMinor);
            Assert.Equal(2, restored.Items.Single().RequestedQuantity);
            Assert.Equal("Submitted", restored.Status);
        }
        finally { await factory.DisposeAsync(); factory.DeleteDatabase(); }
    }
}
