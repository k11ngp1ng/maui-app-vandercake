using System.Net;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using VanderCake.Contracts.Api;
using VanderCake.Domain.Finance;
using VanderCake.Domain.Inventory;
using VanderCake.Domain.Orders;
using VanderCake.Infrastructure.Persistence;
using VanderCake.Server.Configuration;

namespace VanderCake.IntegrationTests;

public sealed class WorkflowTests : IAsyncLifetime
{
    private readonly WorkflowFaultInterceptor _fault = new();
    private readonly ApiFactory _factory;
    private static readonly DateOnly Day = new(2026, 10, 19);

    public WorkflowTests() => _factory = new(configure: services =>
        services.AddDbContext<VanderCakeDbContext>(options => options.AddInterceptors(_fault)));
    public Task InitializeAsync() { SetDay(Day); return _factory.SeedAsync(); }
    public async Task DisposeAsync() { await _factory.DisposeAsync(); _factory.DeleteDatabase(); }
    private void SetDay(DateOnly date) => _factory.Clock.Current = new(date.Year, date.Month, date.Day, 15, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task ALC01_RequiredManualSplitCompletesOrderToPaymentAndPreservesHistory()
    {
        using var seller = await _factory.LoginAsync();
        var customers = (await seller.GetFromJsonAsync<List<CustomerResponse>>("/api/customers"))!;
        var orders = new[] { await Submit(5, customers[0].Id), await Submit(8, customers[1].Id), await Submit(9, customers[2].Id) };
        using var admin = await _factory.LoginAsync("admin@vandercake.local");
        Assert.Equal(3, (await admin.GetFromJsonAsync<List<OrderResponse>>("/api/admin/orders/submitted"))!.Count);
        var initialStock = (await admin.GetFromJsonAsync<List<StockResponse>>("/api/admin/stock"))!.Single(s => s.ProductId == _factory.ProductId);
        Assert.Equal(15, initialStock.EligibleQuantity);
        Assert.Equal(18, initialStock.PhysicalQuantity);
        Assert.Equal(3, initialStock.ExpiredQuantity);
        var result = await Confirm(new(Guid.NewGuid(),
            [Selection(orders[0], 4), Selection(orders[1], 5), Selection(orders[2], 6)]));
        Assert.Equal(15, result.Orders.Sum(o => o.Order.Items.Sum(i => i.FulfilledQuantity)));
        Assert.Equal(7, result.Orders.Sum(o => o.Order.Items.Sum(i => i.UnfulfilledQuantity)));
        Assert.Equal(4, result.Orders.Single(o => o.Order.Id == orders[0].Id).Order.Items.Single().FulfilledQuantity);
        Assert.Equal(5, result.Orders.Single(o => o.Order.Id == orders[1].Id).Order.Items.Single().FulfilledQuantity);
        Assert.Equal(6, result.Orders.Single(o => o.Order.Id == orders[2].Id).Order.Items.Single().FulfilledQuantity);
        Assert.All(result.Orders, o => Assert.Equal("PartiallyFulfilled", o.Order.Status));
        Assert.All(result.Orders, o => Assert.NotNull(o.SaleId));
        Assert.Equal(18750, result.Orders.Sum(o => o.SaleTotalMinor));
        Assert.Equal(18750, result.Orders.SelectMany(o => o.Installments).Sum(i => i.PrincipalMinor));
        var moves = result.Orders.SelectMany(o => o.Movements).ToList();
        Assert.Equal(6, moves.Where(m => m.LotCode == "DEMO-EARLY").Sum(m => m.Quantity));
        Assert.Equal(9, moves.Where(m => m.LotCode == "DEMO-LATER").Sum(m => m.Quantity));
        Assert.DoesNotContain(moves, m => m.LotCode == "DEMO-EXPIRED");
        Assert.All(moves, m => { Assert.Equal(result.AdministratorId, m.AdministratorId); Assert.Equal(result.ConfirmedAtUtc, m.ConsumedAtUtc); });
        Assert.Empty((await admin.GetFromJsonAsync<List<OrderResponse>>("/api/admin/orders/submitted"))!);
        var stock = (await admin.GetFromJsonAsync<List<StockResponse>>("/api/admin/stock"))!.Single(s => s.ProductId == _factory.ProductId);
        Assert.Equal(0, stock.EligibleQuantity);
        Assert.Equal(3, stock.PhysicalQuantity);
        var purchases = (await seller.GetFromJsonAsync<List<SaleResponse>>($"/api/customers/{customers[0].Id}/purchases"))!;
        var sale = Assert.Single(purchases);
        Assert.Equal(4, sale.Items.Single().Quantity);
        Assert.Equal(1250, sale.Items.Single().UnitPriceMinor);
        Assert.Equal(5000, sale.TotalMinor);
        SetDay(new DateOnly(2026, 10, 26));
        using var currentAdmin = await _factory.LoginAsync("admin@vandercake.local");
        var installment = (await currentAdmin.GetFromJsonAsync<ReceivableResponse>($"/api/receivables/{sale.Installments[0].Id}"))!;
        Assert.Equal(2500, installment.PayableMinor);
        var payment = await Pay(installment);
        Assert.Equal(2500, payment.PaidMinor);
        var balance = (await currentAdmin.GetFromJsonAsync<CustomerBalanceResponse>($"/api/customers/{customers[0].Id}/balance"))!;
        Assert.Equal(2500, balance.OpenPrincipalMinor);
        Assert.Equal(0, balance.OverduePrincipalMinor);
        Assert.Equal("Paid", balance.Installments.Single(a => a.Id == installment.Id).Status);
        Assert.Equal(0, balance.Installments.Single(a => a.Id == installment.Id).PayableMinor);
        Assert.Equal(payment.Id, balance.Installments.Single(a => a.Id == installment.Id).Payment!.Id);
    }

    [Theory]
    [InlineData(16, 16)]
    [InlineData(5, 6)]
    [InlineData(5, -1)]
    public async Task ALC02_RejectsOverStockOverRequestAndNegativeWithoutEffects(int requested, int approved)
    {
        var order = await Submit(requested);
        using var admin = await _factory.LoginAsync("admin@vandercake.local");
        await ApiTests.Problem(await admin.PostAsJsonAsync("/api/admin/allocations/confirm",
            new ConfirmAllocationRequest(Guid.NewGuid(), [Selection(order, approved)])), HttpStatusCode.BadRequest);
        await AssertUnchanged(order.Id);
    }

    [Fact]
    public async Task ALC01_AnotherAdministratorChosenSplitIsEquallyValid()
    {
        var a = await Submit(5); var b = await Submit(8); var c = await Submit(9);
        var result = await Confirm(new(Guid.NewGuid(), [Selection(a, 5), Selection(b, 8), Selection(c, 2)]));
        Assert.Equal(5, result.Orders.Single(o => o.Order.Id == a.Id).Order.Items.Single().FulfilledQuantity);
        Assert.Equal(8, result.Orders.Single(o => o.Order.Id == b.Id).Order.Items.Single().FulfilledQuantity);
        Assert.Equal(2, result.Orders.Single(o => o.Order.Id == c.Id).Order.Items.Single().FulfilledQuantity);
        Assert.Equal(15, result.Orders.Sum(o => o.Order.Items.Sum(i => i.FulfilledQuantity)));
    }

    [Fact]
    public async Task ALC02_BatchValidatesCombinedStockAndRollsBackAllOrders()
    {
        var a = await Submit(10); var b = await Submit(10);
        using var admin = await _factory.LoginAsync("admin@vandercake.local");
        await ApiTests.Problem(await admin.PostAsJsonAsync("/api/admin/allocations/confirm",
            new ConfirmAllocationRequest(Guid.NewGuid(), [Selection(a, 10), Selection(b, 10)])), HttpStatusCode.BadRequest);
        await AssertUnchanged(a.Id);
        await AssertUnchanged(b.Id);
    }

    [Fact]
    public async Task STK02_FefoAndFutureDispatchExcludeTooEarlyLots()
    {
        var order = await Submit(5);
        var result = await Confirm(new(Guid.NewGuid(), [Selection(order, 5)]));
        Assert.Equal("DEMO-EARLY", Assert.Single(result.Orders.Single().Movements).LotCode);
        var future = await Submit(10, dispatchDate: Day.AddDays(10));
        using var admin = await _factory.LoginAsync("admin@vandercake.local");
        await ApiTests.Problem(await admin.PostAsJsonAsync("/api/admin/allocations/confirm",
            new ConfirmAllocationRequest(Guid.NewGuid(), [Selection(future, 10)])), HttpStatusCode.BadRequest);
        var approved = await Confirm(new(Guid.NewGuid(), [Selection(future, 9)]));
        Assert.Equal("DEMO-LATER", Assert.Single(approved.Orders.Single().Movements).LotCode);
    }

    [Fact]
    public async Task STK02_ExpiredPhysicalLotsCannotBeAllocated()
    {
        var order = await Submit(1);
        await using (var scope = _factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<VanderCakeDbContext>();
            db.InventoryLots.RemoveRange(await db.InventoryLots.Where(l => l.ProductId == _factory.ProductId && l.Code != "DEMO-EXPIRED").ToListAsync());
            await db.SaveChangesAsync();
        }
        using var admin = await _factory.LoginAsync("admin@vandercake.local");
        await ApiTests.Problem(await admin.PostAsJsonAsync("/api/admin/allocations/confirm",
            new ConfirmAllocationRequest(Guid.NewGuid(), [Selection(order, 1)])), HttpStatusCode.BadRequest);
        var stock = (await admin.GetFromJsonAsync<List<StockResponse>>("/api/admin/stock"))!.Single(s => s.ProductId == _factory.ProductId);
        Assert.Equal(3, stock.PhysicalQuantity); Assert.Equal(0, stock.EligibleQuantity);
    }

    [Fact]
    public async Task ALC03_DuplicateSameRequestIsIdempotentAndChangedOrRepeatedFinalizationConflicts()
    {
        var order = await Submit(5);
        var request = new ConfirmAllocationRequest(Guid.NewGuid(), [Selection(order, 4)]);
        var first = await Confirm(request);
        using var admin = await _factory.LoginAsync("admin@vandercake.local");
        var retry = await admin.PostAsJsonAsync("/api/admin/allocations/confirm", request);
        Assert.Equal(HttpStatusCode.OK, retry.StatusCode);
        Assert.Equal(first.Id, (await retry.Content.ReadFromJsonAsync<AllocationResponse>())!.Id);
        await ApiTests.Problem(await admin.PostAsJsonAsync("/api/admin/allocations/confirm",
            request with { Orders = [Selection(order, 3)] }), HttpStatusCode.Conflict);
        await ApiTests.Problem(await admin.PostAsJsonAsync("/api/admin/allocations/confirm",
            request with { RequestKey = Guid.NewGuid() }), HttpStatusCode.Conflict);
        Assert.Equal(HttpStatusCode.MethodNotAllowed, (await admin.PutAsJsonAsync($"/api/orders/{order.Id}", new { })).StatusCode);
        await using var scope = _factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<VanderCakeDbContext>();
        Assert.Equal(4, await db.LotConsumptions.SumAsync(m => m.Quantity));
        Assert.Single(await db.Sales.ToListAsync()); Assert.Equal(2, await db.AccountsReceivable.CountAsync());
    }

    [Fact]
    public async Task ALC03_ZeroFulfillmentIsAuditedWithoutSalesOrDebt()
    {
        var order = await Submit(5);
        var result = await Confirm(new(Guid.NewGuid(), [Selection(order, 0)]));
        var finalized = Assert.Single(result.Orders);
        Assert.Equal("Cut", finalized.Order.Status); Assert.Null(finalized.SaleId);
        Assert.Empty(finalized.Movements); Assert.Empty(finalized.Installments);
        Assert.Equal(5, finalized.Order.Items.Single().UnfulfilledQuantity);
        using var admin = await _factory.LoginAsync("admin@vandercake.local");
        Assert.Equal(result.Id, (await admin.GetFromJsonAsync<AllocationResponse>($"/api/admin/allocations/{result.Id}"))!.Id);
        var balance = (await admin.GetFromJsonAsync<CustomerBalanceResponse>($"/api/customers/{order.CustomerId}/balance"))!;
        Assert.Equal(0, balance.OpenPrincipalMinor); Assert.Empty(balance.Installments);
    }

    [Fact]
    public async Task ALC03_InjectedFinanceFailureRollsBackAlreadyFlushedInventoryAndOrder()
    {
        var order = await Submit(5);
        _fault.FailFinance = true;
        using var admin = await _factory.LoginAsync("admin@vandercake.local");
        await ApiTests.Problem(await admin.PostAsJsonAsync("/api/admin/allocations/confirm",
            new ConfirmAllocationRequest(Guid.NewGuid(), [Selection(order, 4)])), HttpStatusCode.InternalServerError);
        Assert.True(_fault.SawInventoryFlush);
        await AssertUnchanged(order.Id);
    }

    [Fact]
    public async Task ALC03_ConcurrentAllocationsCannotOversell()
    {
        var a = await Submit(10); var b = await Submit(10);
        using var admin = await _factory.LoginAsync("admin@vandercake.local");
        var start = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        async Task<HttpResponseMessage> Send(OrderResponse order)
        {
            await start.Task;
            return await admin.PostAsJsonAsync("/api/admin/allocations/confirm",
                new ConfirmAllocationRequest(Guid.NewGuid(), [Selection(order, 10)]));
        }
        var first = Send(a); var second = Send(b); start.SetResult();
        var responses = await Task.WhenAll(first, second);
        Assert.Equal(1, responses.Count(r => r.StatusCode == HttpStatusCode.Created));
        Assert.Equal(1, responses.Count(r => r.StatusCode == HttpStatusCode.BadRequest));
        foreach (var response in responses) response.Dispose();
        await using var scope = _factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<VanderCakeDbContext>();
        Assert.Single(await db.Sales.ToListAsync());
        Assert.Equal(10, await db.LotConsumptions.SumAsync(m => m.Quantity));
        Assert.Equal(5, await db.InventoryLots.Where(l => l.ProductId == _factory.ProductId && l.Code != "DEMO-EXPIRED").SumAsync(l => l.AvailableQuantity));
    }

    [Fact]
    public async Task ALC03_StaleVersionAndElapsedWindowRequireReview()
    {
        var order = await Submit(5);
        using var admin = await _factory.LoginAsync("admin@vandercake.local");
        await ApiTests.Problem(await admin.PostAsJsonAsync("/api/admin/allocations/confirm",
            new ConfirmAllocationRequest(Guid.NewGuid(), [Selection(order, 4) with { Version = Guid.NewGuid() }])), HttpStatusCode.Conflict);
        SetDay(Day.AddDays(2));
        using var currentAdmin = await _factory.LoginAsync("admin@vandercake.local");
        await ApiTests.Problem(await currentAdmin.PostAsJsonAsync("/api/admin/allocations/confirm",
            new ConfirmAllocationRequest(Guid.NewGuid(), [Selection(order, 4)])), HttpStatusCode.BadRequest);
        var result = await Confirm(new(Guid.NewGuid(),
            [Selection(order, 4) with { DispatchDate = Day.AddDays(3), DispatchHour = 19 }]));
        Assert.Equal(Day.AddDays(3), result.Orders.Single().Order.DispatchDate);
        Assert.Equal(19, result.Orders.Single().Order.DispatchHour);
    }

    [Fact]
    public async Task FIN02_235ReaisSaleUsesSaleDateAndExact714Schedule()
    {
        var product = await ProductWithStock(235m);
        var order = await Submit(1, productId: product.Id);
        var allocation = await Confirm(new(Guid.NewGuid(), [Selection(order, 1)]));
        using var seller = await _factory.LoginAsync();
        var sale = (await seller.GetFromJsonAsync<SaleResponse>($"/api/sales/{allocation.Orders.Single().SaleId}"))!;
        Assert.Equal(Day, sale.SaleDate); Assert.Equal(23500, sale.TotalMinor);
        Assert.Equal(new long[] { 11750, 11750 }, sale.Installments.Select(a => a.PrincipalMinor));
        Assert.Equal(new[] { new DateOnly(2026, 10, 26), new DateOnly(2026, 11, 2) }, sale.Installments.Select(a => a.DueDate));
        Assert.Equal(sale.Id, sale.Installments[0].SaleId);
    }

    [Fact]
    public async Task FIN02_ThreeInstallmentsKeepExactRemainder()
    {
        var product = await ProductWithStock(100.01m);
        var order = await Submit(1, productId: product.Id, term: 20);
        var result = await Confirm(new(Guid.NewGuid(), [Selection(order, 1)]));
        var accounts = result.Orders.Single().Installments;
        Assert.Equal(new long[] { 3333, 3333, 3335 }, accounts.Select(a => a.PrincipalMinor));
        Assert.Equal(10001, accounts.Sum(a => a.PrincipalMinor));
        Assert.Equal(new[] { Day.AddDays(7), Day.AddDays(14), Day.AddDays(21) }, accounts.Select(a => a.DueDate));
    }

    [Fact]
    public async Task FIN04_PaymentIdempotencyMismatchAndConcurrentDoublePaymentAreSafe()
    {
        var account = await CreateAccount();
        SetDay(account.DueDate);
        var quote = await Account(account.Id);
        var request = PaymentRequest(quote);
        var payment = await Pay(quote, request);
        using var admin = await _factory.LoginAsync("admin@vandercake.local");
        var retry = await admin.PostAsJsonAsync($"/api/admin/receivables/{quote.Id}/payments", request);
        Assert.Equal(HttpStatusCode.OK, retry.StatusCode);
        Assert.Equal(payment.Id, (await retry.Content.ReadFromJsonAsync<PaymentResponse>())!.Id);
        await ApiTests.Problem(await admin.PostAsJsonAsync($"/api/admin/receivables/{quote.Id}/payments",
            request with { AmountMinor = request.AmountMinor + 1 }), HttpStatusCode.Conflict);
        await ApiTests.Problem(await admin.PostAsJsonAsync($"/api/admin/receivables/{quote.Id}/payments",
            request with { RequestKey = Guid.NewGuid() }), HttpStatusCode.Conflict);
        var second = (await admin.GetFromJsonAsync<List<ReceivableResponse>>($"/api/receivables?customerId={quote.CustomerId}"))!.Single(a => a.Status != "Paid");
        var start = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        async Task<HttpResponseMessage> Send()
        {
            await start.Task;
            return await admin.PostAsJsonAsync($"/api/admin/receivables/{second.Id}/payments", PaymentRequest(second));
        }
        var a = Send(); var b = Send(); start.SetResult();
        var responses = await Task.WhenAll(a, b);
        Assert.Equal(1, responses.Count(r => r.StatusCode == HttpStatusCode.Created));
        Assert.Equal(1, responses.Count(r => r.StatusCode == HttpStatusCode.Conflict));
        foreach (var response in responses) response.Dispose();
        var balance = (await admin.GetFromJsonAsync<CustomerBalanceResponse>($"/api/customers/{quote.CustomerId}/balance"))!;
        Assert.Equal(0, balance.OpenPrincipalMinor); Assert.Equal(0, balance.PayableMinor);
    }

    [Fact]
    public async Task FIN04_InjectedPaymentHistoryFailureRollsBackPaidState()
    {
        var account = await CreateAccount();
        _fault.FailPayment = true;
        using var admin = await _factory.LoginAsync("admin@vandercake.local");
        await ApiTests.Problem(await admin.PostAsJsonAsync($"/api/admin/receivables/{account.Id}/payments",
            PaymentRequest(account)), HttpStatusCode.InternalServerError);
        Assert.True(_fault.SawPaidFlush);
        var unchanged = await Account(account.Id);
        Assert.Equal("Open", unchanged.Status); Assert.Equal(account.Version, unchanged.Version);
        Assert.Null(unchanged.Payment);
        await using var scope = _factory.Services.CreateAsyncScope();
        Assert.Equal(0, await scope.ServiceProvider.GetRequiredService<VanderCakeDbContext>().Payments.CountAsync());
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(1)]
    public async Task FIN04_UnderAndOverPaymentCannotSettle(int delta)
    {
        var account = await CreateAccount();
        using var admin = await _factory.LoginAsync("admin@vandercake.local");
        await ApiTests.Problem(await admin.PostAsJsonAsync($"/api/admin/receivables/{account.Id}/payments",
            PaymentRequest(account) with { AmountMinor = account.PayableMinor + delta }), HttpStatusCode.BadRequest);
        Assert.Equal("Open", (await Account(account.Id)).Status);
    }

    [Fact]
    public async Task FIN04_QuoteDateAndVersionAreConcurrencyAssertionsNotClientControlledDates()
    {
        var account = await CreateAccount();
        using var admin = await _factory.LoginAsync("admin@vandercake.local");
        await ApiTests.Problem(await admin.PostAsJsonAsync($"/api/admin/receivables/{account.Id}/payments",
            PaymentRequest(account) with { QuoteDate = Day.AddDays(1) }), HttpStatusCode.Conflict);
        await ApiTests.Problem(await admin.PostAsJsonAsync($"/api/admin/receivables/{account.Id}/payments",
            PaymentRequest(account) with { Version = Guid.NewGuid() }), HttpStatusCode.Conflict);
        Assert.Equal("Open", (await Account(account.Id)).Status);
    }

    [Fact]
    public async Task FIN03_DebtQuotesShowDueEarlyLateAndRetainRateSnapshotAcrossConfigChange()
    {
        var account = await CreateAccount();
        SetDay(account.DueDate.AddDays(2));
        var late = await Account(account.Id);
        Assert.Equal("Overdue", late.Status);
        Assert.Equal(10900, late.PayableMinor);
        Assert.Equal(500, late.PenaltyMinor); Assert.Equal(400, late.InterestMinor);
        using (var admin = await _factory.LoginAsync("admin@vandercake.local"))
        {
            var balance = (await admin.GetFromJsonAsync<CustomerBalanceResponse>($"/api/customers/{account.CustomerId}/balance"))!;
            Assert.Equal(20000, balance.OpenPrincipalMinor);
            Assert.Equal(10000, balance.OverduePrincipalMinor);
            Assert.Equal(20400, balance.PayableMinor);
        }
        await _factory.DisposeAsync();
        await using var restarted = new ApiFactory(_factory.DatabasePath, services => services.PostConfigure<FinanceOptions>(o =>
            { o.PenaltyBasisPoints = 0; o.DailyInterestBasisPoints = 0; o.DailyDiscountBasisPoints = 0; }));
        restarted.Clock.Current = _factory.Clock.Current;
        using var current = await restarted.LoginAsync("admin@vandercake.local", _factory.AdminPassword);
        Assert.Equal(0, (await current.GetFromJsonAsync<FinanceRulesResponse>("/api/admin/finance/rules"))!.PenaltyBasisPoints);
        var preserved = (await current.GetFromJsonAsync<ReceivableResponse>($"/api/receivables/{account.Id}"))!;
        Assert.Equal(10900, preserved.PayableMinor); Assert.Equal(500, preserved.PenaltyBasisPoints);
    }

    [Fact]
    public async Task SEC03_OnlyAdminCanMutateAndOtherPortfoliosCannotReadFinance()
    {
        var account = await CreateAccount();
        var paid = await Pay(account);
        using var seller = await _factory.LoginAsync();
        using var other = await _factory.LoginAsync("other@vandercake.local");
        await ApiTests.Problem(await seller.GetAsync("/api/admin/orders/submitted"), HttpStatusCode.Forbidden);
        await ApiTests.Problem(await seller.GetAsync("/api/admin/stock"), HttpStatusCode.Forbidden);
        await ApiTests.Problem(await seller.PostAsJsonAsync("/api/admin/allocations/confirm",
            new ConfirmAllocationRequest(Guid.NewGuid(), [])), HttpStatusCode.Forbidden);
        await ApiTests.Problem(await seller.PostAsJsonAsync($"/api/admin/receivables/{account.Id}/payments",
            PaymentRequest(account)), HttpStatusCode.Forbidden);
        foreach (var path in new[] { $"/api/customers/{account.CustomerId}/balance",
            $"/api/customers/{account.CustomerId}/purchases", $"/api/receivables/{account.Id}",
            $"/api/sales/{account.SaleId}", $"/api/payments/{paid.Id}" })
            await ApiTests.Problem(await other.GetAsync(path), HttpStatusCode.NotFound);
        Assert.Empty((await other.GetFromJsonAsync<List<ReceivableResponse>>("/api/receivables"))!);
        using var anonymous = _factory.Client();
        await ApiTests.Problem(await anonymous.GetAsync("/api/receivables"), HttpStatusCode.Unauthorized);
    }

    private async Task<OrderResponse> Submit(int quantity, Guid? customerId = null, Guid? productId = null,
        int term = 10, DateOnly? dispatchDate = null)
    {
        using var seller = await _factory.LoginAsync();
        using var response = await seller.PostAsJsonAsync("/api/orders",
            new CreateOrderRequest(customerId ?? _factory.CustomerId, term, dispatchDate ?? Day.AddDays(1),
                19, Guid.NewGuid(), [new(productId ?? _factory.ProductId, quantity)]));
        Assert.True(response.StatusCode == HttpStatusCode.Created, await response.Content.ReadAsStringAsync());
        return (await response.Content.ReadFromJsonAsync<OrderResponse>())!;
    }
    private static OrderAllocationRequest Selection(OrderResponse order, int quantity)
        => new(order.Id, order.Version, [new(order.Items.Single().Id, quantity)]);
    private async Task<AllocationResponse> Confirm(ConfirmAllocationRequest request)
    {
        using var admin = await _factory.LoginAsync("admin@vandercake.local");
        using var response = await admin.PostAsJsonAsync("/api/admin/allocations/confirm", request);
        Assert.True(response.StatusCode == HttpStatusCode.Created, await response.Content.ReadAsStringAsync());
        return (await response.Content.ReadFromJsonAsync<AllocationResponse>())!;
    }
    private async Task<ProductResponse> ProductWithStock(decimal price)
    {
        using var admin = await _factory.LoginAsync("admin@vandercake.local");
        using var response = await admin.PostAsJsonAsync("/api/products",
            new ProductRequest("P" + Guid.NewGuid().ToString("N"), "Test product", _factory.GroupId, "unit", price));
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var product = (await response.Content.ReadFromJsonAsync<ProductResponse>())!;
        await using var scope = _factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<VanderCakeDbContext>();
        db.InventoryLots.Add(new InventoryLot(product.Id, "TEST", Day, Day.AddDays(30), 15));
        await db.SaveChangesAsync();
        return product;
    }
    private async Task<ReceivableResponse> CreateAccount()
    {
        var product = await ProductWithStock(200m);
        var order = await Submit(1, productId: product.Id);
        var result = await Confirm(new(Guid.NewGuid(), [Selection(order, 1)]));
        return await Account(result.Orders.Single().Installments[0].Id);
    }
    private async Task<ReceivableResponse> Account(Guid id)
    {
        using var admin = await _factory.LoginAsync("admin@vandercake.local");
        return (await admin.GetFromJsonAsync<ReceivableResponse>($"/api/receivables/{id}"))!;
    }
    private static RegisterPaymentRequest PaymentRequest(ReceivableResponse account)
        => new(Guid.NewGuid(), account.Version, account.QuoteDate, account.PayableMinor);
    private async Task<PaymentResponse> Pay(ReceivableResponse account, RegisterPaymentRequest? request = null)
    {
        using var admin = await _factory.LoginAsync("admin@vandercake.local");
        using var response = await admin.PostAsJsonAsync($"/api/admin/receivables/{account.Id}/payments", request ?? PaymentRequest(account));
        Assert.True(response.StatusCode == HttpStatusCode.Created, await response.Content.ReadAsStringAsync());
        return (await response.Content.ReadFromJsonAsync<PaymentResponse>())!;
    }
    private async Task AssertUnchanged(Guid orderId)
    {
        await using var scope = _factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<VanderCakeDbContext>();
        Assert.Equal(OrderStatus.Submitted, (await db.Orders.SingleAsync(o => o.Id == orderId)).Status);
        Assert.Equal(15, await db.InventoryLots.Where(l => l.ProductId == _factory.ProductId && l.Code != "DEMO-EXPIRED").SumAsync(l => l.AvailableQuantity));
        Assert.Equal(0, await db.LotConsumptions.CountAsync()); Assert.Equal(0, await db.Sales.CountAsync());
        Assert.Equal(0, await db.AccountsReceivable.CountAsync()); Assert.Equal(0, await db.AllocationBatches.CountAsync());
    }
}
