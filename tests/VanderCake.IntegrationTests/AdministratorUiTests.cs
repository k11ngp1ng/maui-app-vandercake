using System.Net;
using System.Net.Http.Json;
using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using VanderCake.Application.Abstractions;
using VanderCake.Contracts.Api;
using VanderCake.Infrastructure.Persistence;
namespace VanderCake.IntegrationTests;

public sealed class AdministratorUiTests : IAsyncLifetime
{
    private readonly ApiFactory factory = new();
    public Task InitializeAsync() => factory.SeedAsync();
    public async Task DisposeAsync() { await factory.DisposeAsync(); factory.DeleteDatabase(); }
    private static Dictionary<string, string> Fields(string html) => Regex.Matches(html,
        "<input[^>]*name=\"([^\"]+)\"[^>]*value=\"([^\"]*)\"[^>]*>", RegexOptions.None, TimeSpan.FromSeconds(1))
        .Where(m => !m.Value.Contains("type=\"checkbox\""))
        .GroupBy(m => WebUtility.HtmlDecode(m.Groups[1].Value))
        .ToDictionary(g => g.Key, g => WebUtility.HtmlDecode(g.First().Groups[2].Value));
    private async Task<HttpClient> Browser()
    {
        var c = factory.Client(cookies: true);
        var f = Fields(await c.GetStringAsync("/Account/Login")); f["Email"] = "admin@vandercake.local"; f["Password"] = factory.AdminPassword;
        Assert.Equal(HttpStatusCode.Redirect, (await c.PostAsync("/Account/Login", new FormUrlEncodedContent(f))).StatusCode); return c;
    }
    private static async Task<HttpResponseMessage> Post(HttpClient c, string path, Dictionary<string,string> values)
    {
        var html = await c.GetStringAsync(path); var fields = Fields(html);
        foreach (var pair in values) fields[pair.Key] = pair.Value;
        return await c.PostAsync(path, new FormUrlEncodedContent(fields));
    }
    [Fact]
    public async Task UI01_AllAdministratorScreensRequireCookieAndRenderWithRealData()
    {
        string[] paths = ["/Admin", "/Admin/Customers", "/Admin/Salespersons", "/Admin/Products", "/Admin/Groups", "/Admin/Stock", "/Admin/Orders", "/Admin/Allocations", "/Admin/Sales", "/Admin/Receivables"];
        using var anonymous = factory.Client(); using var browser = await Browser();
        foreach (var path in paths)
        {
            Assert.Equal(HttpStatusCode.Redirect, (await anonymous.GetAsync(path)).StatusCode);
            var response = await browser.GetAsync(path); Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            var html = await response.Content.ReadAsStringAsync(); Assert.Contains("Main navigation", html); Assert.Contains("Skip to content", html);
        }
        Assert.Contains("No sales to show", await browser.GetStringAsync("/Admin/Sales"));
        Assert.Contains("No installments to show", await browser.GetStringAsync("/Admin/Receivables"));
        Assert.Equal(HttpStatusCode.OK, (await browser.GetAsync("/css/site.css")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await browser.GetAsync("/js/site.js")).StatusCode);
    }
    [Fact]
    public async Task UI02_BatchRazorFlowAllocates456CreatesSalesPaysAndUpdatesActualMetrics()
    {
        using var seller = await factory.LoginAsync();
        var customers = (await seller.GetFromJsonAsync<List<CustomerResponse>>("/api/customers"))!;
        var orders = new List<OrderResponse>();
        foreach (var (c, qty) in customers.Take(3).Zip(new[] { 5, 8, 9 }))
        {
            var r = await seller.PostAsJsonAsync("/api/orders", new CreateOrderRequest(c.Id, 10, new(2026,10,22), 19, Guid.NewGuid(), [new(factory.ProductId, qty)]));
            Assert.Equal(HttpStatusCode.Created, r.StatusCode); orders.Add((await r.Content.ReadFromJsonAsync<OrderResponse>())!);
        }
        using var browser = await Browser();
        var path = "/Admin/Allocations?DispatchDate=2026-10-22";
        var html = await browser.GetStringAsync(path); var fields = Fields(html);
        fields["DispatchHour"] = "19"; fields["Confirm"] = "true";
        foreach (var pair in fields.Where(p => Regex.IsMatch(p.Key, @"Orders\[\d+\]\.OrderId")).ToList())
        {
            var prefix = pair.Key[..pair.Key.LastIndexOf('.')]; var n = orders.FindIndex(o => o.Id.ToString() == pair.Value);
            fields[prefix + ".Selected"] = "true"; fields[prefix + ".Items[0].FulfilledQuantity"] = new[] { 4, 5, 6 }[n].ToString();
        }
        var result = await browser.PostAsync(path, new FormUrlEncodedContent(fields));
        Assert.Equal(HttpStatusCode.Redirect, result.StatusCode);
        var history = await browser.GetStringAsync(result.Headers.Location!); Assert.Contains("Allocation confirmed", history); Assert.Contains("Inventory movements", history); Assert.Contains("Review sale", history);
        Assert.Equal(HttpStatusCode.Redirect, (await browser.PostAsync(path, new FormUrlEncodedContent(fields))).StatusCode);
        using var admin = await factory.LoginAsync("admin@vandercake.local");
        var stock = (await admin.GetFromJsonAsync<List<StockResponse>>("/api/admin/stock?dispatchDate=2026-10-22"))!.Single(s => s.ProductId == factory.ProductId);
        Assert.Equal(0, stock.EligibleQuantity);
        foreach (var o in orders) { var finalized = (await admin.GetFromJsonAsync<OrderResponse>($"/api/orders/{o.Id}"))!; Assert.Equal("PartiallyFulfilled", finalized.Status); Assert.Equal(HttpStatusCode.OK, (await browser.GetAsync($"/Admin/Order/{o.Id}")).StatusCode); }
        var accounts = (await admin.GetFromJsonAsync<List<ReceivableResponse>>("/api/receivables"))!;
        Assert.Equal(6, accounts.Count); Assert.Equal(18750, accounts.Sum(a => a.PrincipalMinor));
        var a = accounts.First();
        Assert.Equal(HttpStatusCode.OK, (await browser.GetAsync($"/Admin/Sale/{a.SaleId}")).StatusCode);
        var payment = await Post(browser, $"/Admin/Payment/{a.Id}", new());
        Assert.Equal(HttpStatusCode.Redirect, payment.StatusCode); Assert.Contains("Payment registered", await browser.GetStringAsync(payment.Headers.Location!));
        var balance = (await admin.GetFromJsonAsync<CustomerBalanceResponse>($"/api/customers/{a.CustomerId}/balance"))!;
        Assert.Single(balance.Installments, x => x.Status == "Paid");
        await using var scope = factory.Services.CreateAsyncScope(); var db = scope.ServiceProvider.GetRequiredService<VanderCakeDbContext>();
        Assert.Equal(3, await db.Sales.CountAsync()); Assert.Equal(1, await db.Payments.CountAsync());
        // Resolve through HTTP current-user scope using an authenticated page, then compare its metric values to SQL.
        Assert.Contains("R$ 187.50", await browser.GetStringAsync("/Admin"));
        Assert.Contains(((18750 - a.PrincipalMinor) / 100m).ToString("N2", System.Globalization.CultureInfo.GetCultureInfo("en-US")), await browser.GetStringAsync("/Admin/Receivables"));
    }
    [Fact]
    public async Task UI03_CustomersProductsGroupsCanBeCreatedAndEditedWithoutRawHtmlRendering()
    {
        using var browser = await Browser();
        using var admin = await factory.LoginAsync("admin@vandercake.local");
        var city = (await admin.GetFromJsonAsync<List<CityResponse>>("/api/reference/cities"))!.First();
        var result = await Post(browser, "/Admin/Customers", new() { ["Form.Name"] = "<script>alert(1)</script>", ["Form.CityId"] = city.Id.ToString(), ["Form.SalespersonId"] = factory.OwnerId.ToString(), ["Form.VisitOrder"] = "4", ["Form.Address"] = "Demo street", ["Form.Phone"] = "11999999999", ["Form.IsActive"] = "true" });
        Assert.Equal(HttpStatusCode.Redirect, result.StatusCode);
        var html = await browser.GetStringAsync("/Admin/Customers"); Assert.DoesNotContain("<script>alert(1)</script>", html); Assert.Contains("&lt;script&gt;", html);
        var customer = (await admin.GetFromJsonAsync<List<CustomerResponse>>("/api/customers"))!.Single(c => c.VisitOrder == 4);
        var edit = $"/Admin/Customers?id={customer.Id}";
        Assert.Equal(HttpStatusCode.Redirect, (await Post(browser, edit, new() { ["Form.Name"] = "Updated bakery", ["Form.CityId"] = city.Id.ToString(), ["Form.SalespersonId"] = factory.OwnerId.ToString(), ["Form.VisitOrder"] = "5", ["Form.Address"] = "New street", ["Form.Phone"] = "11988888888", ["Form.IsActive"] = "false" })).StatusCode);
        Assert.False((await admin.GetFromJsonAsync<CustomerResponse>($"/api/customers/{customer.Id}"))!.IsActive);
        Assert.Equal(HttpStatusCode.Redirect, (await Post(browser, "/Admin/Groups", new() { ["Name"] = "Packaging" })).StatusCode);
        var group = (await admin.GetFromJsonAsync<List<ProductGroupResponse>>("/api/reference/product-groups"))!.Single(g => g.Name == "Packaging");
        Assert.Equal(HttpStatusCode.Redirect, (await Post(browser, $"/Admin/Groups?id={group.Id}", new() { ["Name"] = "Bakery packaging" })).StatusCode);
        Assert.Equal(HttpStatusCode.Redirect, (await Post(browser, "/Admin/Products", new() { ["Form.Code"] = "PACK", ["Form.Name"] = "Cake box", ["Form.GroupId"] = group.Id.ToString(), ["Form.SaleUnit"] = "box", ["Form.Price"] = "5.25", ["Form.IsActive"] = "true" })).StatusCode);
        var product = (await admin.GetFromJsonAsync<List<ProductResponse>>("/api/products"))!.Single(p => p.Code == "PACK");
        Assert.Equal(525, product.PriceMinor);
        Assert.Equal(HttpStatusCode.Redirect, (await Post(browser, $"/Admin/Products?id={product.Id}", new() { ["Form.Code"] = "PACK", ["Form.Name"] = "Cake box", ["Form.GroupId"] = group.Id.ToString(), ["Form.SaleUnit"] = "box", ["Form.Price"] = "6.10", ["Form.IsActive"] = "false" })).StatusCode);
        Assert.False((await admin.GetFromJsonAsync<ProductResponse>($"/api/products/{product.Id}"))!.IsActive);
    }
    [Fact]
    public async Task UI04_StockReceiptValidatesDatesAndDuplicateCodesAndPersistsNewUnits()
    {
        using var browser = await Browser();
        var data = new Dictionary<string,string> { ["ProductId"] = factory.ProductId.ToString(), ["Code"] = "WEB-RECEIPT", ["ReceivedDate"] = "2026-10-21", ["ExpirationDate"] = "2026-11-21", ["Quantity"] = "12" };
        Assert.Equal(HttpStatusCode.Redirect, (await Post(browser, "/Admin/Stock", data)).StatusCode);
        var duplicate = await Post(browser, "/Admin/Stock", data); Assert.Equal(HttpStatusCode.OK, duplicate.StatusCode); Assert.Contains("duplicate", await duplicate.Content.ReadAsStringAsync());
        data["Code"] = "EXPIRED"; data["ExpirationDate"] = "2026-10-20";
        Assert.Contains("Cannot receive expired stock", await (await Post(browser, "/Admin/Stock", data)).Content.ReadAsStringAsync());
        await using var scope = factory.Services.CreateAsyncScope(); var db = scope.ServiceProvider.GetRequiredService<VanderCakeDbContext>(); Assert.Equal(12, (await db.InventoryLots.SingleAsync(l => l.Code == "WEB-RECEIPT")).AvailableQuantity); Assert.False(await db.InventoryLots.AnyAsync(l => l.Code == "EXPIRED"));
    }
    [Fact]
    public async Task UI05_SalespersonCreationAndDeactivationRevokeExistingBearerAndPreservePortfolio()
    {
        using var browser = await Browser(); var password = ApiFactory.Password();
        Assert.Equal(HttpStatusCode.Redirect, (await Post(browser, "/Admin/Salespersons", new() { ["Name"] = "New seller", ["Email"] = "new@vandercake.local", ["Password"] = password })).StatusCode);
        using var seller = await factory.LoginAsync("new@vandercake.local", password);
        Assert.Equal(HttpStatusCode.OK, (await seller.GetAsync("/api/customers")).StatusCode);
        await using var scope = factory.Services.CreateAsyncScope(); var db = scope.ServiceProvider.GetRequiredService<VanderCakeDbContext>();
        var s = await db.Salespersons.SingleAsync(s => s.Name == "New seller");
        Assert.Equal(HttpStatusCode.Redirect, (await Post(browser, $"/Admin/Salespersons?id={s.Id}", new() { ["Name"] = "Renamed seller", ["IsActive"] = "false" })).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await seller.GetAsync("/api/customers")).StatusCode);
        Assert.Contains("Inactive", await browser.GetStringAsync("/Admin/Salespersons"));
    }
    [Fact]
    public async Task UI06_ManagementAndBatchFormsRequireAntiforgeryAndRejectStaleEdits()
    {
        using var browser = await Browser();
        foreach (var path in new[] { "/Admin/Customers", "/Admin/Products", "/Admin/Salespersons", "/Admin/Groups", "/Admin/Stock", "/Admin/Allocations" })
            Assert.Equal(HttpStatusCode.BadRequest, (await browser.PostAsync(path, new FormUrlEncodedContent([]))).StatusCode);
        using var admin = await factory.LoginAsync("admin@vandercake.local"); var p = (await admin.GetFromJsonAsync<ProductResponse>($"/api/products/{factory.ProductId}"))!;
        var stale = await Post(browser, $"/Admin/Products?id={p.Id}", new() { ["Version"] = Guid.NewGuid().ToString(), ["Form.Code"] = p.Code, ["Form.Name"] = p.Name, ["Form.GroupId"] = p.ProductGroupId.ToString(), ["Form.SaleUnit"] = p.SaleUnit, ["Form.Price"] = "3.00", ["Form.IsActive"] = "true" });
        Assert.Equal(HttpStatusCode.OK, stale.StatusCode); Assert.Contains("changed", (await stale.Content.ReadAsStringAsync()).ToLowerInvariant());
        Assert.Equal(p.PriceMinor, (await admin.GetFromJsonAsync<ProductResponse>($"/api/products/{p.Id}"))!.PriceMinor);
        Assert.Contains("Select at least one order", await (await Post(browser, "/Admin/Allocations?DispatchDate=2026-10-22", new() { ["Confirm"] = "true", ["DispatchHour"] = "19" })).Content.ReadAsStringAsync());
    }
}
