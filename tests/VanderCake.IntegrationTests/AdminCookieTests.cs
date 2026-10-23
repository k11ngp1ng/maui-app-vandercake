using System.Net;
using System.Net.Http.Json;
using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using VanderCake.Contracts.Api;
using VanderCake.Infrastructure.Persistence;

namespace VanderCake.IntegrationTests;

public sealed class AdminCookieTests : IAsyncLifetime
{
    private readonly ApiFactory _factory = new();
    public Task InitializeAsync() => _factory.SeedAsync();
    public async Task DisposeAsync() { await _factory.DisposeAsync(); _factory.DeleteDatabase(); }

    [Fact]
    public async Task SEC02_AdminPagesChallengeAndLoginUsesSecureCookie()
    {
        using var client = _factory.Client(cookies: true);
        var denied = await client.GetAsync("/Admin");
        Assert.Equal(HttpStatusCode.Redirect, denied.StatusCode);
        Assert.Contains("/Account/Login", denied.Headers.Location!.OriginalString);
        var response = await SignIn(client, "admin@vandercake.local", _factory.AdminPassword, "https://evil.example");
        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.Equal("/Admin", response.Headers.Location!.OriginalString);
        var cookies = string.Join(";", response.Headers.GetValues("Set-Cookie"));
        Assert.Contains("VanderCake.Admin", cookies);
        Assert.Contains("secure", cookies.ToLowerInvariant());
        Assert.Contains("httponly", cookies.ToLowerInvariant());
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/Admin")).StatusCode);
        await ApiTests.Problem(await client.GetAsync("/api/customers"), HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task SEC02_LoginAndLogoutRequireAntiforgery()
    {
        using var client = _factory.Client(cookies: true);
        var missing = await client.PostAsync("/Account/Login", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["Email"] = "admin@vandercake.local", ["Password"] = _factory.AdminPassword
        }));
        Assert.Equal(HttpStatusCode.BadRequest, missing.StatusCode);
        var login = await SignIn(client, "admin@vandercake.local", _factory.AdminPassword);
        Assert.Equal(HttpStatusCode.Redirect, login.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await client.PostAsync("/Admin/Logout", new FormUrlEncodedContent([]))).StatusCode);
        var token = await Token(client, "/Admin");
        Assert.Equal(HttpStatusCode.Redirect, (await client.PostAsync("/Admin/Logout",
            new FormUrlEncodedContent(new Dictionary<string, string> { ["__RequestVerificationToken"] = token }))).StatusCode);
        Assert.Equal(HttpStatusCode.Redirect, (await client.GetAsync("/Admin")).StatusCode);
    }

    [Fact]
    public async Task SEC02_SalespersonCannotObtainAdministratorCookie()
    {
        using var client = _factory.Client(cookies: true);
        var response = await SignIn(client, "salesperson@vandercake.local", _factory.SellerPassword);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("Invalid credentials", await response.Content.ReadAsStringAsync());
        Assert.False(response.Headers.TryGetValues("Set-Cookie", out _));
        Assert.Equal(HttpStatusCode.Redirect, (await client.GetAsync("/Admin")).StatusCode);
        using var bearer = await _factory.LoginAsync("admin@vandercake.local");
        Assert.Equal(HttpStatusCode.Redirect, (await bearer.GetAsync("/Admin")).StatusCode);
    }

    [Fact]
    public async Task SEC02_DisablingAdministratorInvalidatesCookie()
    {
        using var client = _factory.Client(cookies: true);
        Assert.Equal(HttpStatusCode.Redirect,
            (await SignIn(client, "admin@vandercake.local", _factory.AdminPassword)).StatusCode);
        await using var scope = _factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<VanderCakeDbContext>();
        var user = await db.Users.SingleAsync(x => x.Email == "admin@vandercake.local");
        user.IsActive = false; await db.SaveChangesAsync();
        Assert.Equal(HttpStatusCode.Redirect, (await client.GetAsync("/Admin")).StatusCode);
    }

    private static async Task<HttpResponseMessage> SignIn(HttpClient client, string email, string password, string returnUrl = "")
    {
        var token = await Token(client, "/Account/Login");
        return await client.PostAsync("/Account/Login", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["Email"] = email, ["Password"] = password, ["__RequestVerificationToken"] = token, ["ReturnUrl"] = returnUrl
        }));
    }

    private static async Task<string> Token(HttpClient client, string path)
    {
        var html = await client.GetStringAsync(path);
        var match = Regex.Match(html, "name=\"__RequestVerificationToken\"[^>]*value=\"([^\"]+)\"",
            RegexOptions.None, TimeSpan.FromSeconds(1));
        Assert.True(match.Success, "Expected a real Razor antiforgery token.");
        return WebUtility.HtmlDecode(match.Groups[1].Value);
    }
}
