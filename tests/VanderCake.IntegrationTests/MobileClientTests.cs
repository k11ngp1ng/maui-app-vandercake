using System.Net;
using System.Net.Http.Json;
using System.Text.RegularExpressions;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using VanderCake.Contracts.Api;
using VanderCake.Mobile.Client;
using VanderCake.Mobile.ViewModels;
namespace VanderCake.IntegrationTests;

public sealed class MobileClientTests : IAsyncLifetime
{
    private readonly ApiFactory factory = new();
    public Task InitializeAsync() => factory.SeedAsync();
    public async Task DisposeAsync() { await factory.DisposeAsync(); factory.DeleteDatabase(); }
    private sealed class Settings : IConnectionSettings
    { public string Address { get; set; } = "https://localhost/"; public string DevelopmentPin { get; set; } = ""; public bool IsDevelopment => true; }
    private sealed class Vault : ITokenVault
    { public string? Value; public Task<string?> ReadAsync() => Task.FromResult(Value); public Task WriteAsync(string value) { Value = value; return Task.CompletedTask; } public void Clear() => Value = null; }
    private sealed class Navigation : IAppNavigation
    {
        public string? Route; public Guid? Id; public bool Accept = true;
        public Task LoginAsync() { Route = "login"; return Task.CompletedTask; }
        public Task PortfolioAsync() { Route = "portfolio"; return Task.CompletedTask; }
        public Task OpenAsync(string route, Guid? id = null) { Route = route; Id = id; return Task.CompletedTask; }
        public Task BackAsync() => Task.CompletedTask;
        public Task<bool> ConfirmAsync(string title, string message, string accept) => Task.FromResult(Accept);
    }
    private sealed class Handler(Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> send) : HttpMessageHandler
    { protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct) => send(request, ct); }
    [Fact]
    public async Task MOB01_ExactClientAndViewModelsLoginPortfolioDetailsSubmitStatusAndAdminVisibility()
    {
        using var http = factory.Client(); var settings = new Settings(); var vault = new Vault(); var nav = new Navigation();
        var api = new SalesApi(http, vault, settings, factory.Clock);
        var login = new LoginViewModel(api, settings, nav) { Email = "salesperson@vandercake.local", Password = factory.SellerPassword };
        await login.SignInAsync(); Assert.Equal("", login.Error); Assert.Equal("portfolio", nav.Route); Assert.Equal("", login.Password); Assert.NotNull(vault.Value);
        var portfolio = new PortfolioViewModel(api, nav); await portfolio.LoadAsync(); Assert.Equal(3, portfolio.Customers.Count);
        var customer = portfolio.Customers.Single(c => c.Id == factory.CustomerId);
        await portfolio.OpenCustomerCommand.ExecuteAsync(customer); Assert.Equal(customer.Id, nav.Id);
        var details = new CustomerViewModel(api, nav) { Id = customer.Id }; await details.LoadAsync(); Assert.Equal(customer.VisitOrder, details.Customer!.VisitOrder); Assert.Equal(0, details.Balance!.OpenPrincipalMinor);
        var order = new NewOrderViewModel(api, nav, factory.Clock) { Id = customer.Id };
        await order.LoadAsync(); Assert.Equal("", order.Error);
        order.Products.Single(p => p.Product.Id == factory.ProductId).Quantity = "2";
        order.SelectedTerm = order.Terms.Single(t => t.Code == 10); order.DispatchDate = new(2026,10,22);
        await order.SubmitAsync(); Assert.Equal("", order.Error); Assert.Equal("order", nav.Route);
        var id = nav.Id!.Value;
        var status = new OrderViewModel(api, nav) { Id = id }; await status.LoadAsync();
        Assert.Equal("Submitted", status.Order!.Status); Assert.Equal(2500, status.Order.RequestedTotalMinor);
        var history = new OrdersViewModel(api, nav); await history.LoadAsync(); Assert.Single(history.Orders);
        using var browser = factory.Client(cookies:true);
        var html = await browser.GetStringAsync("/Account/Login");
        var token = WebUtility.HtmlDecode(Regex.Match(html, "name=\"__RequestVerificationToken\"[^>]*value=\"([^\"]+)\"", RegexOptions.None, TimeSpan.FromSeconds(1)).Groups[1].Value);
        Assert.Equal(HttpStatusCode.Redirect, (await browser.PostAsync("/Account/Login", new FormUrlEncodedContent(new Dictionary<string,string> { ["__RequestVerificationToken"] = token, ["Email"] = "admin@vandercake.local", ["Password"] = factory.AdminPassword }))).StatusCode);
        Assert.Contains(id.ToString(), await browser.GetStringAsync("/Admin/Orders"));
        Assert.Contains(customer.Name, await browser.GetStringAsync($"/Admin/Order/{id}"));
    }
    [Fact]
    public async Task MOB02_RegistrationAssignmentHistoricalPricesAndDebtUseServerData()
    {
        using var http = factory.Client(); var api = new SalesApi(http, new Vault(), new Settings(), factory.Clock); var nav = new Navigation();
        await api.LoginAsync("salesperson@vandercake.local", factory.SellerPassword, default);
        var registration = new RegistrationViewModel(api, nav); await registration.LoadAsync();
        registration.SelectedState = registration.States.First(); registration.SelectedCity = registration.Cities.First();
        registration.Name = "Mobile bakery"; registration.Address = "Demo road 5"; registration.Phone = "11912345678"; registration.VisitOrder = "4";
        await registration.SaveAsync(); Assert.Equal("", registration.Error); Assert.Equal("customer", nav.Route);
        var created = await api.CustomerAsync(nav.Id!.Value, default); Assert.Equal(factory.OwnerId, created.SalespersonId);
        var response = await api.SubmitAsync(new(factory.CustomerId,10,new(2026,10,22),19,Guid.NewGuid(),[new(factory.ProductId,2)]), default);
        using var admin = await factory.LoginAsync("admin@vandercake.local");
        var allocation = await admin.PostAsJsonAsync("/api/admin/allocations/confirm", new ConfirmAllocationRequest(Guid.NewGuid(),[new(response.Id,response.Version,[new(response.Items[0].Id,1)])]));
        Assert.Equal(HttpStatusCode.Created, allocation.StatusCode);
        var purchases = new PurchasesViewModel(api,nav) { Id = factory.CustomerId }; await purchases.LoadAsync();
        Assert.Single(purchases.Purchases); Assert.Equal(1250,purchases.Purchases[0].TotalMinor); Assert.Equal(1,purchases.Purchases[0].Items[0].Quantity);
        var balance = await api.BalanceAsync(factory.CustomerId,default); Assert.Equal(1250,balance.OpenPrincipalMinor);
        Assert.Equal("PartiallyFulfilled",(await api.OrderAsync(response.Id,default)).Status);
    }
    [Fact]
    public async Task MOB03_SessionRestoresOnlyForSameOriginAndRejectsAdministrator()
    {
        using var http = factory.Client(); var vault = new Vault(); var settings = new Settings();
        var first = new SalesApi(http,vault,settings,factory.Clock); await first.LoginAsync("salesperson@vandercake.local",factory.SellerPassword,default);
        var restored = new SalesApi(http,vault,settings,factory.Clock); Assert.True(await restored.RestoreAsync(default));
        settings.Address = "https://another.example/";
        var rejected = await Assert.ThrowsAsync<ClientFailure>(() => restored.CustomersAsync(default)); Assert.True(rejected.AuthenticationExpired); Assert.Null(vault.Value);
        settings.Address = "https://localhost/";
        await Assert.ThrowsAsync<ClientFailure>(() => first.LoginAsync("admin@vandercake.local",factory.AdminPassword,default)); Assert.Null(vault.Value);
    }
    [Fact]
    public async Task MOB04_CancellationNetworkErrorsBadLoginAndExpiredSessionAreHandled()
    {
        var vault = new Vault(); var settings = new Settings();
        using var http = new HttpClient(new Handler((_,_) => throw new HttpRequestException("untrusted internal network detail")));
        var api = new SalesApi(http,vault,settings,factory.Clock);
        var failure = await Assert.ThrowsAsync<ClientFailure>(() => api.LoginAsync("seller@example.com","unused",default)); Assert.Contains("Cannot reach",failure.Message); Assert.DoesNotContain("internal",failure.Message);
        using var cancelledHttp = new HttpClient(new Handler(async (_,ct) => { await Task.Delay(Timeout.Infinite,ct); return new(HttpStatusCode.OK); }));
        using var cancelled = new CancellationTokenSource(); cancelled.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => new SalesApi(cancelledHttp,vault,settings,factory.Clock).LoginAsync("seller@example.com","unused",cancelled.Token));
        using var real = factory.Client(); var actual = new SalesApi(real,vault,settings,factory.Clock);
        await Assert.ThrowsAsync<ClientFailure>(() => actual.LoginAsync("salesperson@vandercake.local",ApiFactory.Password(),default));
        await actual.LoginAsync("salesperson@vandercake.local",factory.SellerPassword,default);
        factory.Clock.Current = factory.Clock.Current.AddHours(1);
        Assert.True((await Assert.ThrowsAsync<ClientFailure>(() => actual.CustomersAsync(default))).AuthenticationExpired); Assert.Null(vault.Value);
    }
    [Theory]
    [InlineData("http://localhost:5080")]
    [InlineData("https://user:password@example.com")]
    [InlineData("https://example.com/api")]
    [InlineData("https://example.com?token=private")]
    public void MOB05_UnsafeOrAmbiguousEndpointsAreRejected(string address) => Assert.Throws<ClientFailure>(() => ApiConnection.Validate(address));
    [Fact]
    public void MOB06_DevelopmentTrustRequiresExactValidLocalCertificatePin()
    {
        using var key = RSA.Create(2048); var request = new CertificateRequest("CN=localhost", key, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
        using var cert = request.CreateSelfSigned(DateTimeOffset.UtcNow.AddDays(-1), DateTimeOffset.UtcNow.AddDays(1));
        var pin = cert.GetCertHashString(HashAlgorithmName.SHA256); var now = DateTimeOffset.UtcNow;
        Assert.True(ApiConnection.MatchesLocalPin(new("https://10.0.2.2:7080"),cert,pin,now));
        Assert.False(ApiConnection.MatchesLocalPin(new("https://external.example"),cert,pin,now));
        Assert.False(ApiConnection.MatchesLocalPin(new("https://localhost"),cert,"",now));
        Assert.False(ApiConnection.MatchesLocalPin(new("https://localhost"),cert,pin,now.AddDays(2)));
        Assert.False(ApiConnection.MatchesLocalPin(new("http://localhost"),cert,pin,now));
    }
    private sealed class LoseFirstOrderResponse(HttpMessageHandler inner) : DelegatingHandler(inner)
    {
        private bool lost;
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            var response = await base.SendAsync(request, ct);
            if (!lost && request.Method == HttpMethod.Post && request.RequestUri!.AbsolutePath == "/api/orders" && response.IsSuccessStatusCode)
            { lost = true; response.Dispose(); throw new HttpRequestException("Simulated lost response after commit"); }
            return response;
        }
    }
    [Fact]
    public async Task MOB07_LostCommittedResponseFreezesDraftAndRetriesSameOrderWithoutDuplication()
    {
        using var http = new HttpClient(new LoseFirstOrderResponse(factory.Server.CreateHandler()));
        var api = new SalesApi(http,new Vault(),new Settings(),factory.Clock); var nav = new Navigation();
        await api.LoginAsync("salesperson@vandercake.local",factory.SellerPassword,default);
        var vm = new NewOrderViewModel(api,nav,factory.Clock) { Id = factory.CustomerId };
        await vm.LoadAsync(); vm.SelectedTerm = vm.Terms.First(); vm.DispatchDate = new(2026,10,22);
        vm.Products.Single(p => p.Product.Id == factory.ProductId).Quantity = "2";
        await vm.SubmitAsync(); Assert.True(vm.HasPendingSubmission); Assert.False(vm.IsEditable); Assert.Contains("Cannot reach",vm.Error);
        await vm.SubmitAsync(); Assert.Equal("",vm.Error); Assert.Equal("order",nav.Route);
        var orders = await api.OrdersAsync(default); Assert.Single(orders); Assert.Equal(nav.Id,orders[0].Id); Assert.False(vm.HasPendingSubmission);
    }
    private sealed class WaitingPage(IAppNavigation nav) : PageViewModel(nav)
    { public override Task LoadAsync() => RunAsync(ct => Task.Delay(Timeout.Infinite, ct)); }
    [Fact]
    public async Task MOB08_PageNavigationCancelsQuietlyButExplicitCancelShowsFeedback()
    {
        var page = new WaitingPage(new Navigation());
        var navigating = page.LoadAsync(); Assert.True(page.IsBusy); page.Deactivate(); await navigating;
        Assert.False(page.HasNotice); Assert.False(page.IsBusy);
        var requested = page.LoadAsync(); page.Cancel(); await requested;
        Assert.True(page.HasNotice); Assert.Contains("Request cancelled", page.Notice); Assert.False(page.IsBusy);
    }
}


