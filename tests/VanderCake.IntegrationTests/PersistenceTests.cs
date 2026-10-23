using System.Net.Http.Json;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using VanderCake.Contracts.Api;
using VanderCake.Domain.Common;
using VanderCake.Domain.Finance;
using VanderCake.Domain.Orders;
using VanderCake.Infrastructure.Persistence;

namespace VanderCake.IntegrationTests;

public sealed class PersistenceTests : IAsyncLifetime
{
    private readonly ApiFactory _factory = new();
    public Task InitializeAsync() => _factory.SeedAsync();
    public async Task DisposeAsync() { await _factory.DisposeAsync(); _factory.DeleteDatabase(); }

    [Fact]
    public async Task DB01_MigrationHasNoPendingChangesAndReferenceSeedIsDeterministic()
    {
        await using var scope = _factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<VanderCakeDbContext>();
        Assert.Equal(2, (await db.Database.GetAppliedMigrationsAsync()).Count());
        Assert.Empty(await db.Database.GetPendingMigrationsAsync());
        Assert.False(db.Database.HasPendingModelChanges());
        Assert.Equal(2, await db.States.CountAsync());
        Assert.Equal(3, await db.Cities.CountAsync());
        Assert.Equal(5, await db.InstallmentDefinitions.CountAsync());
        var term = await db.PaymentTerms.Include(x => x.Definitions).SingleAsync(x => x.Code == 20);
        Assert.Equal(new long[] { 3333, 3333, 3334 }, term.Schedule(10000, new DateOnly(2026, 10, 21)).Select(x => x.PrincipalMinor));
    }

    [Fact]
    public async Task DB02_RealSqliteRejectsNegativeStockAndInvalidPrice()
    {
        await using var scope = _factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<VanderCakeDbContext>();
        var lot = await db.InventoryLots.FirstAsync(x => x.ProductId == _factory.ProductId);
        await Assert.ThrowsAsync<SqliteException>(() => db.Database.ExecuteSqlInterpolatedAsync(
            $"UPDATE InventoryLots SET AvailableQuantity = {-1} WHERE Id = {lot.Id}"));
        await Assert.ThrowsAsync<SqliteException>(() => db.Database.ExecuteSqlInterpolatedAsync(
            $"UPDATE Products SET PriceMinor = {0} WHERE Id = {_factory.ProductId}"));
        await Assert.ThrowsAsync<SqliteException>(() => db.Database.ExecuteSqlInterpolatedAsync(
            $"UPDATE Customers SET CityId = {Guid.NewGuid()} WHERE Id = {_factory.CustomerId}"));
    }

    [Fact]
    public async Task DB03_TwoContextsCannotSilentlyOverwriteCustomer()
    {
        await using var firstScope = _factory.Services.CreateAsyncScope();
        await using var secondScope = _factory.Services.CreateAsyncScope();
        var first = firstScope.ServiceProvider.GetRequiredService<VanderCakeDbContext>();
        var second = secondScope.ServiceProvider.GetRequiredService<VanderCakeDbContext>();
        var a = await first.Customers.SingleAsync(x => x.Id == _factory.CustomerId);
        var b = await second.Customers.SingleAsync(x => x.Id == _factory.CustomerId);
        a.Update("First edit", a.SalespersonId, a.CityId, a.VisitOrder, a.Address, a.Phone, true);
        b.Update("Second edit", b.SalespersonId, b.CityId, b.VisitOrder, b.Address, b.Phone, true);
        await first.SaveChangesAsync();
        await Assert.ThrowsAsync<DbUpdateConcurrencyException>(() => second.SaveChangesAsync());
    }

    [Fact]
    public async Task DB04_OrderSurvivesDisposingAndRestartingSeparateHost()
    {
        using var seller = await _factory.LoginAsync();
        var request = new CreateOrderRequest(_factory.CustomerId, 10, new DateOnly(2026, 10, 22), 19,
            Guid.NewGuid(), [new(_factory.ProductId, 2)]);
        var response = await seller.PostAsJsonAsync("/api/orders", request);
        var order = (await response.Content.ReadFromJsonAsync<OrderResponse>())!;
        await _factory.DisposeAsync();
        await using var restarted = new ApiFactory(_factory.DatabasePath);
        using var client = await restarted.LoginAsync("salesperson@vandercake.local", _factory.SellerPassword);
        var restored = (await client.GetFromJsonAsync<OrderResponse>($"/api/orders/{order.Id}"))!;
        Assert.Equal(order.Id, restored.Id);
        Assert.Equal(2500, restored.RequestedTotalMinor);
        Assert.Single((await client.GetFromJsonAsync<List<OrderResponse>>("/api/orders"))!);
    }

    [Fact]
    public async Task DB05_SaleReceivableAndPaymentGraphRoundTripsWithIntegerMoney()
    {
        using var seller = await _factory.LoginAsync();
        var response = await seller.PostAsJsonAsync("/api/orders", new CreateOrderRequest(_factory.CustomerId, 20,
            new DateOnly(2026, 10, 22), 19, Guid.NewGuid(), [new(_factory.ProductId, 8)]));
        var dto = (await response.Content.ReadFromJsonAsync<OrderResponse>())!;
        await using var scope = _factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<VanderCakeDbContext>();
        var order = await db.Orders.Include(x => x.Items).SingleAsync(x => x.Id == dto.Id);
        order.ConfirmQuantities(new Dictionary<Guid, int> { [order.Items.Single().Id] = 8 });
        var date = new DateOnly(2026, 10, 21);
        var sale = new Sale(order, date);
        db.Sales.Add(sale);
        var term = await db.PaymentTerms.Include(x => x.Definitions).SingleAsync(x => x.Code == 20);
        var receivables = term.Schedule(sale.TotalMinor, date).Select(x =>
            new AccountsReceivable(sale.Id, x, date, new FinancialRules())).ToArray();
        db.AccountsReceivable.AddRange(receivables);
        var adminId = await db.Users.Where(x => x.Email == "admin@vandercake.local").Select(x => x.Id).SingleAsync();
        var payment = receivables[0].Settle(receivables[0].Quote(date).AmountMinor, date,
            adminId, Guid.NewGuid(), ApiFactory.Now.UtcDateTime);
        db.Payments.Add(payment);
        await db.SaveChangesAsync();
        db.ChangeTracker.Clear();
        Assert.Equal(10000, (await db.Sales.Include(x => x.Items).SingleAsync()).TotalMinor);
        Assert.Equal(10000, await db.AccountsReceivable.SumAsync(x => x.PrincipalMinor));
        Assert.Equal(FinancialStatus.Paid, (await db.AccountsReceivable.SingleAsync(x => x.Id == receivables[0].Id)).Status);
        Assert.Equal(payment.PaidMinor, (await db.Payments.SingleAsync()).PaidMinor);
        await using var command = db.Database.GetDbConnection().CreateCommand();
        await db.Database.OpenConnectionAsync();
        command.CommandText = "SELECT typeof(PriceMinor) FROM Products LIMIT 1";
        Assert.Equal("integer", await command.ExecuteScalarAsync());
    }
}
