using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.BearerToken;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using VanderCake.Contracts.Api;
using VanderCake.Infrastructure.Identity;
using VanderCake.Infrastructure.Persistence;

namespace VanderCake.IntegrationTests;

public sealed class ApiTests : IAsyncLifetime
{
    private readonly ApiFactory _factory = new();
    public Task InitializeAsync() => _factory.SeedAsync();
    public async Task DisposeAsync() { await _factory.DisposeAsync(); _factory.DeleteDatabase(); }

    [Fact]
    public async Task SEC01_AnonymousAndInvalidCredentialsAreStructured()
    {
        using var client = _factory.Client();
        await Problem(await client.GetAsync("/api/customers"), HttpStatusCode.Unauthorized);
        await Problem(await client.PostAsJsonAsync("/api/auth/login",
            new { email = "salesperson@vandercake.local", password = ApiFactory.Password() }), HttpStatusCode.Unauthorized);
        await Problem(await client.PostAsJsonAsync("/api/auth/login",
            new { email = "missing@vandercake.local", password = ApiFactory.Password() }), HttpStatusCode.Unauthorized);
        Assert.Contains((await client.PostAsJsonAsync("/api/auth/register", new { })).StatusCode,
            new[] { HttpStatusCode.NotFound, HttpStatusCode.MethodNotAllowed });
    }

    [Fact]
    public async Task SEC01_LockoutRejectsCorrectPasswordAfterFiveFailures()
    {
        using var client = _factory.Client();
        for (var i = 0; i < 5; i++)
            Assert.Equal(HttpStatusCode.Unauthorized, (await client.PostAsJsonAsync("/api/auth/login",
                new { email = "salesperson@vandercake.local", password = ApiFactory.Password() })).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.PostAsJsonAsync("/api/auth/login",
            new { email = "salesperson@vandercake.local", password = _factory.SellerPassword })).StatusCode);
    }

    [Fact]
    public async Task SEC02_SalespersonCannotWriteProductsOrReadSalespersonDirectory()
    {
        using var seller = await _factory.LoginAsync();
        await Problem(await seller.PostAsJsonAsync("/api/products", Product()), HttpStatusCode.Forbidden);
        await Problem(await seller.GetAsync("/api/reference/salespersons"), HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task SEC03_CustomerPortfolioAndForgedOwnerAreProtected()
    {
        using var seller = await _factory.LoginAsync();
        using var other = await _factory.LoginAsync("other@vandercake.local");
        var customers = await seller.GetFromJsonAsync<List<CustomerResponse>>("/api/customers");
        Assert.Equal(new[] { 1, 2, 3 }, customers!.Select(x => x.VisitOrder));
        Assert.All(customers!, x => Assert.Equal(_factory.OwnerId, x.SalespersonId));
        Assert.Empty((await other.GetFromJsonAsync<List<CustomerResponse>>("/api/customers"))!);
        await Problem(await other.GetAsync($"/api/customers/{_factory.CustomerId}"), HttpStatusCode.NotFound);
        await Problem(await other.PutAsJsonAsync($"/api/customers/{_factory.CustomerId}",
            new UpdateCustomerRequest(Customer(), Guid.NewGuid())), HttpStatusCode.NotFound);
        await Problem(await seller.PostAsJsonAsync("/api/customers", Customer() with { SalespersonId = _factory.OtherOwnerId }),
            HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task CRM01_CustomerCreateUpdateAndStaleVersionHaveStableContract()
    {
        using var seller = await _factory.LoginAsync();
        using var response = await seller.PostAsJsonAsync("/api/customers", Customer());
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var created = (await response.Content.ReadFromJsonAsync<CustomerResponse>())!;
        Assert.Equal(_factory.OwnerId, created.SalespersonId);
        Assert.Equal($"/api/customers/{created.Id}", response.Headers.Location!.OriginalString);
        var update = new UpdateCustomerRequest(Customer() with { Name = "Updated bakery" }, created.Version);
        var updated = await seller.PutAsJsonAsync($"/api/customers/{created.Id}", update);
        Assert.Equal(HttpStatusCode.OK, updated.StatusCode);
        Assert.Equal("Updated bakery", (await updated.Content.ReadFromJsonAsync<CustomerResponse>())!.Name);
        await Problem(await seller.PutAsJsonAsync($"/api/customers/{created.Id}", update), HttpStatusCode.Conflict);
    }

    [Theory]
    [InlineData("", 1)]
    [InlineData("Customer", 0)]
    public async Task INPUT_InvalidCustomerReturnsValidationProblem(string name, int visit)
    {
        using var seller = await _factory.LoginAsync();
        await Problem(await seller.PostAsJsonAsync("/api/customers", Customer() with { Name = name, VisitOrder = visit }),
            HttpStatusCode.BadRequest, validation: true);
    }

    [Fact]
    public async Task INPUT_MalformedUnknownAndNullBodiesReturn400()
    {
        using var seller = await _factory.LoginAsync();
        await Problem(await seller.PostAsync("/api/orders", new StringContent("{", Encoding.UTF8, "application/json")), HttpStatusCode.BadRequest);
        await Problem(await seller.PostAsJsonAsync("/api/orders", new { CustomerId = _factory.CustomerId, injectedPrice = 1 }), HttpStatusCode.BadRequest);
        await Problem(await seller.PutAsJsonAsync($"/api/customers/{_factory.CustomerId}",
            new { customer = (object?)null, version = Guid.NewGuid() }), HttpStatusCode.BadRequest);
        await Problem(await seller.PostAsJsonAsync("/api/customers", Customer() with { CityId = Guid.NewGuid() }), HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task CAT01_AdminProductPriceRoundsAndDuplicateCodeConflicts()
    {
        using var admin = await _factory.LoginAsync("admin@vandercake.local");
        var request = Product() with { Code = "NEW-PRODUCT", Price = 1.005m };
        var response = await admin.PostAsJsonAsync("/api/products", request);
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var product = (await response.Content.ReadFromJsonAsync<ProductResponse>())!;
        Assert.Equal(101, product.PriceMinor);
        Assert.Equal(1.01m, product.Price);
        await Problem(await admin.PostAsJsonAsync("/api/products", request), HttpStatusCode.Conflict);
        await Problem(await admin.PostAsJsonAsync("/api/products", request with { Code = "INVALID", Price = 0 }), HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task ORD01_SubmissionSnapshotsPricesAndEnforcesOwnershipAndIdempotency()
    {
        using var seller = await _factory.LoginAsync();
        var request = Order();
        var response = await seller.PostAsJsonAsync("/api/orders", request);
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var order = (await response.Content.ReadFromJsonAsync<OrderResponse>())!;
        Assert.Equal("Submitted", order.Status);
        Assert.Equal(6250, order.RequestedTotalMinor);
        Assert.Equal(1250, order.Items.Single().UnitPriceMinor);
        Assert.Equal(HttpStatusCode.OK, (await seller.PostAsJsonAsync("/api/orders", request)).StatusCode);
        await Problem(await seller.PostAsJsonAsync("/api/orders", request with { DispatchHour = 12 }), HttpStatusCode.Conflict);
        using var other = await _factory.LoginAsync("other@vandercake.local");
        await Problem(await other.GetAsync($"/api/orders/{order.Id}"), HttpStatusCode.NotFound);
        await Problem(await other.PostAsJsonAsync("/api/orders", request with { SubmissionKey = Guid.NewGuid() }), HttpStatusCode.NotFound);
        Assert.Empty((await other.GetFromJsonAsync<List<OrderResponse>>("/api/orders"))!);
        using var admin = await _factory.LoginAsync("admin@vandercake.local");
        Assert.Single((await admin.GetFromJsonAsync<List<OrderResponse>>("/api/orders"))!);
        await Problem(await admin.PostAsJsonAsync("/api/orders", request), HttpStatusCode.Forbidden);
        var oldProduct = (await admin.GetFromJsonAsync<ProductResponse>($"/api/products/{_factory.ProductId}"))!;
        Assert.Equal(HttpStatusCode.OK, (await admin.PutAsJsonAsync($"/api/products/{_factory.ProductId}",
            new UpdateProductRequest(Product() with { Code = oldProduct.Code, Name = oldProduct.Name, Price = 99m }, oldProduct.Version))).StatusCode);
        var unchanged = (await seller.GetFromJsonAsync<OrderResponse>($"/api/orders/{order.Id}"))!;
        Assert.Equal(1250, unchanged.Items.Single().UnitPriceMinor);
    }

    [Theory]
    [InlineData(13, 10, 5)]
    [InlineData(12, 30, 5)]
    [InlineData(19, 20, 0)]
    public async Task ORD01_InvalidSlotTermOrQuantityReturns400(int hour, int term, int quantity)
    {
        using var seller = await _factory.LoginAsync();
        await Problem(await seller.PostAsJsonAsync("/api/orders", Order() with
        {
            DispatchHour = hour, PaymentTermCode = term, Items = [new(_factory.ProductId, quantity)]
        }), HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task ORD01_EmptyDuplicatePastAndInactiveOrdersAreRejected()
    {
        using var seller = await _factory.LoginAsync();
        await Problem(await seller.PostAsJsonAsync("/api/orders", Order() with { Items = [] }), HttpStatusCode.BadRequest);
        await Problem(await seller.PostAsJsonAsync("/api/orders", Order() with
            { Items = [new(_factory.ProductId, 1), new(_factory.ProductId, 2)] }), HttpStatusCode.BadRequest);
        await Problem(await seller.PostAsJsonAsync("/api/orders", Order() with
            { DispatchDate = new DateOnly(2026, 10, 20) }), HttpStatusCode.BadRequest);
        await using var scope = _factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<VanderCakeDbContext>();
        var customer = await db.Customers.SingleAsync(x => x.Id == _factory.CustomerId);
        customer.Update(customer.Name, customer.SalespersonId, customer.CityId, customer.VisitOrder, customer.Address, customer.Phone, false);
        await db.SaveChangesAsync();
        await Problem(await seller.PostAsJsonAsync("/api/orders", Order()), HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task SEC03_ReassignedCustomerRevokesOldOwnerOrderAccess()
    {
        using var seller = await _factory.LoginAsync();
        var response = await seller.PostAsJsonAsync("/api/orders", Order());
        var order = (await response.Content.ReadFromJsonAsync<OrderResponse>())!;
        using var admin = await _factory.LoginAsync("admin@vandercake.local");
        var customer = (await admin.GetFromJsonAsync<CustomerResponse>($"/api/customers/{_factory.CustomerId}"))!;
        Assert.Equal(HttpStatusCode.OK, (await admin.PutAsJsonAsync($"/api/customers/{_factory.CustomerId}",
            new UpdateCustomerRequest(Customer() with { SalespersonId = _factory.OtherOwnerId }, customer.Version))).StatusCode);
        await Problem(await seller.GetAsync($"/api/orders/{order.Id}"), HttpStatusCode.NotFound);
        await Problem(await seller.GetAsync($"/api/customers/{customer.Id}"), HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task SEC01_TamperedExpiredAndRevokedBearerTokensAreRejected()
    {
        using var seller = await _factory.LoginAsync();
        var token = seller.DefaultRequestHeaders.Authorization!.Parameter!;
        seller.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token[..^4] + "AAAA");
        await Problem(await seller.GetAsync("/api/customers"), HttpStatusCode.Unauthorized);
        await using var scope = _factory.Services.CreateAsyncScope();
        var users = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var user = (await users.FindByEmailAsync("salesperson@vandercake.local"))!;
        var principal = await scope.ServiceProvider.GetRequiredService<SignInManager<ApplicationUser>>().CreateUserPrincipalAsync(user);
        var options = scope.ServiceProvider.GetRequiredService<IOptionsMonitor<BearerTokenOptions>>().Get(IdentityConstants.BearerScheme);
        var expired = options.BearerTokenProtector.Protect(new AuthenticationTicket(principal,
            new AuthenticationProperties { IssuedUtc = ApiFactory.Now.AddHours(-2), ExpiresUtc = ApiFactory.Now.AddHours(-1) },
            IdentityConstants.BearerScheme));
        seller.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", expired);
        await Problem(await seller.GetAsync("/api/customers"), HttpStatusCode.Unauthorized);
        seller.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        Assert.Equal(HttpStatusCode.NoContent, (await seller.PostAsync("/api/auth/logout", null)).StatusCode);
        await Problem(await seller.GetAsync("/api/customers"), HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task SEC01_DisabledAccountInvalidatesExistingToken()
    {
        using var seller = await _factory.LoginAsync();
        await using var scope = _factory.Services.CreateAsyncScope();
        var users = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var user = (await users.FindByEmailAsync("salesperson@vandercake.local"))!;
        user.IsActive = false;
        Assert.True((await users.UpdateAsync(user)).Succeeded);
        await Problem(await seller.GetAsync("/api/customers"), HttpStatusCode.Forbidden);
        Assert.Equal(HttpStatusCode.Unauthorized, (await _factory.Client().PostAsJsonAsync("/api/auth/login",
            new { email = user.Email, password = _factory.SellerPassword })).StatusCode);
    }

    [Fact]
    public async Task SEC01_RoleChangeInvalidatesPreviouslyIssuedClaims()
    {
        using var seller = await _factory.LoginAsync();
        await using var scope = _factory.Services.CreateAsyncScope();
        var users = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var user = (await users.FindByEmailAsync("salesperson@vandercake.local"))!;
        Assert.True((await users.AddToRoleAsync(user, "Administrator")).Succeeded);
        await Problem(await seller.GetAsync("/api/customers"), HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task SEC01_HttpCannotCarryCredentialsOrBearerApiTraffic()
    {
        using var client = _factory.Client();
        client.BaseAddress = new Uri("http://localhost");
        await Problem(await client.GetAsync("/Account/Login"), HttpStatusCode.BadRequest);
        await Problem(await client.PostAsJsonAsync("/api/auth/login",
            new { email = "salesperson@vandercake.local", password = _factory.SellerPassword }), HttpStatusCode.BadRequest);
    }

    private CustomerRequest Customer() => new("New bakery", ReferenceSeed.SaoPauloCityId, 4, "Demo address", "11900000004");
    private ProductRequest Product() => new("TEST", "Test product", _factory.GroupId, "package", 12.50m);
    private CreateOrderRequest Order() => new(_factory.CustomerId, 10, new DateOnly(2026, 10, 22), 19,
        Guid.NewGuid(), [new(_factory.ProductId, 5)]);

    internal static async Task Problem(HttpResponseMessage response, HttpStatusCode expected, bool validation = false)
    {
        using (response)
        {
            var text = await response.Content.ReadAsStringAsync();
            Assert.True(response.StatusCode == expected, $"Expected {expected}, got {response.StatusCode}: {text}");
            Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
            var json = JsonDocument.Parse(text).RootElement;
            Assert.Equal((int)expected, json.GetProperty("status").GetInt32());
            Assert.True(json.TryGetProperty("traceId", out _));
            if (validation) Assert.True(json.TryGetProperty("errors", out _));
            Assert.DoesNotContain("StackTrace", text);
        }
    }
}
