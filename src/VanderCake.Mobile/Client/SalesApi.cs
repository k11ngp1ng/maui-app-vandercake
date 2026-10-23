using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using VanderCake.Contracts.Api;

namespace VanderCake.Mobile.Client;

public sealed class SalesApi(HttpClient http, ITokenVault vault, IConnectionSettings settings, TimeProvider clock) : ISalesApi
{
    private sealed record SavedSession(string Origin, string Token, DateTimeOffset ExpiresAt);
    private SavedSession? session;
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    private async Task<T> SendAsync<T>(HttpMethod method, string path, object? body, bool authorized, CancellationToken ct)
    {
        var origin = ApiConnection.Validate(settings.Address);
        if (authorized && (session is null || session.Origin != origin.AbsoluteUri || session.ExpiresAt <= clock.GetUtcNow()))
        { session = null; vault.Clear(); throw new ClientFailure("Your session expired. Please sign in again.", true); }
        using var request = new HttpRequestMessage(method, new Uri(origin, path));
        if (body is not null) request.Content = JsonContent.Create(body, body.GetType(), options: Json);
        if (authorized) request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", session!.Token);
        try
        {
            using var response = await http.SendAsync(request, ct);
            if (!response.IsSuccessStatusCode)
            {
                if (response.StatusCode is HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden && authorized)
                { session = null; vault.Clear(); throw new ClientFailure("Your account or session is no longer authorized. Sign in again.", true); }
                string message = response.StatusCode switch {
                    HttpStatusCode.Unauthorized => "Email or password is incorrect.",
                    HttpStatusCode.NotFound => "This record is unavailable. Refresh your portfolio; it may have been reassigned.",
                    HttpStatusCode.Conflict => "The request conflicts with current server data. Refresh and review before retrying.",
                    _ => "The server could not complete the request. Please try again." };
                if ((int)response.StatusCode is 400 or 409)
                {
                    try {
                        var problem = await response.Content.ReadFromJsonAsync<JsonElement>(Json, ct);
                        if (problem.TryGetProperty("detail", out var detail) && detail.ValueKind == JsonValueKind.String) message = detail.GetString()!;
                        else if (problem.TryGetProperty("errors", out var errors)) message = string.Join(" ", errors.EnumerateObject().SelectMany(p => p.Value.EnumerateArray().Select(v => v.GetString())));
                    } catch (JsonException) { /* Keep a safe fallback for non-JSON upstream failures. */ }
                }
                throw new ClientFailure(message);
            }
            if (typeof(T) == typeof(bool)) return (T)(object)true;
            return await response.Content.ReadFromJsonAsync<T>(Json, ct) ?? throw new ClientFailure("The server returned an empty response.");
        }
        catch (HttpRequestException) { throw new ClientFailure("Cannot reach the server. Check your connection, HTTPS certificate and API address, then retry."); }
        catch (OperationCanceledException) when (!ct.IsCancellationRequested) { throw new ClientFailure("The request timed out. Check order history before creating another order; retrying this same submission is safe."); }
        catch (JsonException) { throw new ClientFailure("The server response is incompatible. Check that the app and server versions match."); }
    }
    public async Task LoginAsync(string email, string password, CancellationToken ct)
    {
        session = null; vault.Clear();
        var origin = ApiConnection.Validate(settings.Address);
        var token = await SendAsync<BearerLoginResponse>(HttpMethod.Post, "api/auth/login", new LoginRequest(email, password), false, ct);
        if (token.TokenType != "Bearer" || string.IsNullOrWhiteSpace(token.AccessToken) || token.ExpiresIn <= 0 || token.ExpiresIn > 86400)
            throw new ClientFailure("The server returned an unsupported login response.");
        session = new(origin.AbsoluteUri, token.AccessToken, clock.GetUtcNow().AddSeconds(token.ExpiresIn));
        try {
            var user = await SendAsync<UserResponse>(HttpMethod.Get, "api/auth/me", null, true, ct);
            if (!user.Roles.Contains("Salesperson")) throw new ClientFailure("Use a salesperson account. Administrators use the web portal.");
            await vault.WriteAsync(JsonSerializer.Serialize(session, Json));
        } catch { session = null; vault.Clear(); throw; }
    }
    public async Task<bool> RestoreAsync(CancellationToken ct)
    {
        var saved = await vault.ReadAsync(); if (string.IsNullOrEmpty(saved)) return false;
        try { session = JsonSerializer.Deserialize<SavedSession>(saved, Json); }
        catch (JsonException) { vault.Clear(); return false; }
        try {
            var user = await SendAsync<UserResponse>(HttpMethod.Get, "api/auth/me", null, true, ct);
            if (user.Roles.Contains("Salesperson")) return true;
            session = null; vault.Clear(); return false;
        } catch (ClientFailure e) when (e.AuthenticationExpired) { return false; }
    }
    public async Task LogoutAsync(CancellationToken ct)
    {
        try { if (session is not null) await SendAsync<bool>(HttpMethod.Post, "api/auth/logout", null, true, ct); }
        finally { session = null; vault.Clear(); }
    }
    public Task<List<CustomerResponse>> CustomersAsync(CancellationToken ct) => Get<List<CustomerResponse>>("api/customers", ct);
    public Task<CustomerResponse> CustomerAsync(Guid id, CancellationToken ct) => Get<CustomerResponse>($"api/customers/{id}", ct);
    public Task<CustomerBalanceResponse> BalanceAsync(Guid id, CancellationToken ct) => Get<CustomerBalanceResponse>($"api/customers/{id}/balance", ct);
    public Task<List<SaleResponse>> PurchasesAsync(Guid id, CancellationToken ct) => Get<List<SaleResponse>>($"api/customers/{id}/purchases", ct);
    public Task<List<CityResponse>> CitiesAsync(CancellationToken ct) => Get<List<CityResponse>>("api/reference/cities", ct);
    public Task<List<StateResponse>> StatesAsync(CancellationToken ct) => Get<List<StateResponse>>("api/reference/states", ct);
    public Task<CustomerResponse> CreateCustomerAsync(CustomerRequest request, CancellationToken ct) => SendAsync<CustomerResponse>(HttpMethod.Post, "api/customers", request, true, ct);
    public Task<List<ProductResponse>> ProductsAsync(CancellationToken ct) => Get<List<ProductResponse>>("api/products", ct);
    public Task<List<PaymentTermResponse>> TermsAsync(CancellationToken ct) => Get<List<PaymentTermResponse>>("api/reference/payment-terms", ct);
    public Task<OrderResponse> SubmitAsync(CreateOrderRequest request, CancellationToken ct) => SendAsync<OrderResponse>(HttpMethod.Post, "api/orders", request, true, ct);
    public Task<List<OrderResponse>> OrdersAsync(CancellationToken ct) => Get<List<OrderResponse>>("api/orders", ct);
    public Task<OrderResponse> OrderAsync(Guid id, CancellationToken ct) => Get<OrderResponse>($"api/orders/{id}", ct);
    private Task<T> Get<T>(string path, CancellationToken ct) => SendAsync<T>(HttpMethod.Get, path, null, true, ct);
}

