using System.Net;
using System.Net.Http.Json;
using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using VanderCake.Contracts.Api;
using VanderCake.Server.Configuration;

namespace VanderCake.IntegrationTests;

public sealed class WorkflowEdgeTests : IAsyncLifetime
{
    private readonly ApiFactory _factory = new(configure: services =>
        services.PostConfigure<FinanceOptions>(o => o.DailyDiscountBasisPoints = 10000));
    public Task InitializeAsync() => _factory.SeedAsync();
    public async Task DisposeAsync() { await _factory.DisposeAsync(); _factory.DeleteDatabase(); }

    [Fact]
    public async Task FIN04_ZeroPayableNeedsConfirmationAndThenClearsPrincipal()
    {
        var order = await Submit();
        using var admin = await _factory.LoginAsync("admin@vandercake.local");
        using var response = await admin.PostAsJsonAsync("/api/admin/allocations/confirm",
            new ConfirmAllocationRequest(Guid.NewGuid(), [new(order.Id, order.Version, [new(order.Items.Single().Id, 1)])]));
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var result = (await response.Content.ReadFromJsonAsync<AllocationResponse>())!;
        var id = result.Orders.Single().Installments[0].Id;
        var account = (await admin.GetFromJsonAsync<ReceivableResponse>($"/api/receivables/{id}"))!;
        Assert.Equal(0, account.PayableMinor);
        Assert.Equal(account.PrincipalMinor, account.DiscountMinor);
        var request = new RegisterPaymentRequest(Guid.NewGuid(), account.Version, account.QuoteDate, 0);
        await ApiTests.Problem(await admin.PostAsJsonAsync($"/api/admin/receivables/{id}/payments", request), HttpStatusCode.BadRequest);
        var paid = await admin.PostAsJsonAsync($"/api/admin/receivables/{id}/payments", request with { ConfirmZero = true });
        Assert.Equal(HttpStatusCode.Created, paid.StatusCode);
        var receipt = (await paid.Content.ReadFromJsonAsync<PaymentResponse>())!;
        Assert.Equal(0, receipt.PaidMinor);
        var balance = (await admin.GetFromJsonAsync<CustomerBalanceResponse>($"/api/customers/{order.CustomerId}/balance"))!;
        Assert.Equal(625, balance.OpenPrincipalMinor);
        Assert.Equal(0, balance.PayableMinor);
        Assert.Equal("Paid", balance.Installments.Single(a => a.Id == id).Status);
    }

    [Fact]
    public async Task ALC03_ConcurrentDuplicateConfirmationProducesOneOutcome()
    {
        var order = await Submit();
        var request = new ConfirmAllocationRequest(Guid.NewGuid(), [new(order.Id, order.Version, [new(order.Items.Single().Id, 1)])]);
        using var admin = await _factory.LoginAsync("admin@vandercake.local");
        var responses = await Task.WhenAll(
            admin.PostAsJsonAsync("/api/admin/allocations/confirm", request),
            admin.PostAsJsonAsync("/api/admin/allocations/confirm", request));
        Assert.Contains(responses, r => r.StatusCode == HttpStatusCode.Created);
        Assert.Contains(responses, r => r.StatusCode == HttpStatusCode.OK);
        var outcomes = await Task.WhenAll(responses.Select(r => r.Content.ReadFromJsonAsync<AllocationResponse>()));
        Assert.Equal(outcomes[0]!.Id, outcomes[1]!.Id);
        foreach (var response in responses) response.Dispose();
    }

    [Fact]
    public async Task INPUT_AllocationRejectsDuplicateMissingAndForeignLines()
    {
        var order = await Submit();
        using var admin = await _factory.LoginAsync("admin@vandercake.local");
        var line = new AllocationLineRequest(order.Items.Single().Id, 1);
        foreach (var selected in new[] {
            new OrderAllocationRequest(order.Id, order.Version, []),
            new OrderAllocationRequest(order.Id, order.Version, [line, line]),
            new OrderAllocationRequest(order.Id, order.Version, [new(Guid.NewGuid(), 1)]),
            new OrderAllocationRequest(order.Id, order.Version, [line], DispatchHour: 12) })
            await ApiTests.Problem(await admin.PostAsJsonAsync("/api/admin/allocations/confirm",
                new ConfirmAllocationRequest(Guid.NewGuid(), [selected])), HttpStatusCode.BadRequest);
        Assert.Equal("Submitted", (await admin.GetFromJsonAsync<OrderResponse>($"/api/orders/{order.Id}"))!.Status);
    }

    [Fact]
    public async Task RazorFlow_AdministratorCanAllocateAndRegisterPaymentWithAntiforgery()
    {
        var order = await Submit();
        using var browser = _factory.Client(cookies: true);
        var loginToken = await Token(browser, "/Account/Login");
        var login = await browser.PostAsync("/Account/Login", Form(new()
        {
            ["Email"] = "admin@vandercake.local", ["Password"] = _factory.AdminPassword,
            ["__RequestVerificationToken"] = loginToken
        }));
        Assert.Equal(HttpStatusCode.Redirect, login.StatusCode);
        Assert.Contains(order.CustomerName, await browser.GetStringAsync("/Admin/Orders"));
        var path = $"/Admin/Allocation/{order.Id}";
        var allocationToken = await Token(browser, path);
        var fields = new Dictionary<string, string>
        {
            ["Version"] = order.Version.ToString(), ["RequestKey"] = Guid.NewGuid().ToString(),
            ["DispatchDate"] = order.DispatchDate.ToString("yyyy-MM-dd"), ["DispatchHour"] = "19",
            ["Items[0].OrderItemId"] = order.Items.Single().Id.ToString(), ["Items[0].FulfilledQuantity"] = "1"
        };
        Assert.Equal(HttpStatusCode.BadRequest, (await browser.PostAsync(path, Form(fields))).StatusCode);
        fields["__RequestVerificationToken"] = allocationToken;
        var allocation = await browser.PostAsync(path, Form(fields));
        Assert.Equal(HttpStatusCode.Redirect, allocation.StatusCode);
        Assert.Contains("Allocation confirmed", await browser.GetStringAsync(allocation.Headers.Location!));
        Assert.Contains(order.CustomerName, await browser.GetStringAsync("/Admin/Receivables"));
        using var admin = await _factory.LoginAsync("admin@vandercake.local");
        var account = (await admin.GetFromJsonAsync<List<ReceivableResponse>>("/api/receivables"))!.First();
        var paymentPath = $"/Admin/Payment/{account.Id}";
        var paymentToken = await Token(browser, paymentPath);
        var paymentFields = new Dictionary<string, string>
        {
            ["Version"] = account.Version.ToString(), ["RequestKey"] = Guid.NewGuid().ToString(),
            ["QuoteDate"] = account.QuoteDate.ToString("yyyy-MM-dd"), ["AmountMinor"] = account.PayableMinor.ToString(),
            ["ConfirmZero"] = "true", ["__RequestVerificationToken"] = paymentToken
        };
        var payment = await browser.PostAsync(paymentPath, Form(paymentFields));
        Assert.Equal(HttpStatusCode.Redirect, payment.StatusCode);
        Assert.Contains("State: Paid", await browser.GetStringAsync(payment.Headers.Location!));
        Assert.Equal("Paid", (await admin.GetFromJsonAsync<ReceivableResponse>($"/api/receivables/{account.Id}"))!.Status);
    }

    private async Task<OrderResponse> Submit()
    {
        using var seller = await _factory.LoginAsync();
        using var response = await seller.PostAsJsonAsync("/api/orders", new CreateOrderRequest(_factory.CustomerId,
            10, new DateOnly(2026, 10, 22), 19, Guid.NewGuid(), [new(_factory.ProductId, 1)]));
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<OrderResponse>())!;
    }
    private static FormUrlEncodedContent Form(Dictionary<string, string> fields) => new(fields);
    private static async Task<string> Token(HttpClient client, string path)
    {
        var html = await client.GetStringAsync(path);
        var match = Regex.Match(html, "name=\"__RequestVerificationToken\"[^>]*value=\"([^\"]+)\"",
            RegexOptions.None, TimeSpan.FromSeconds(1));
        Assert.True(match.Success, "Expected real Razor form token.");
        return WebUtility.HtmlDecode(match.Groups[1].Value);
    }
}
