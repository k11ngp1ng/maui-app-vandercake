using VanderCake.Contracts.Api;

namespace VanderCake.Mobile.Client;

public interface ITokenVault
{
    Task<string?> ReadAsync();
    Task WriteAsync(string value);
    void Clear();
}
public interface IConnectionSettings
{
    string Address { get; set; }
    string DevelopmentPin { get; set; }
    bool IsDevelopment { get; }
}
public interface IAppNavigation
{
    Task LoginAsync();
    Task PortfolioAsync();
    Task OpenAsync(string route, Guid? id = null);
    Task BackAsync();
    Task<bool> ConfirmAsync(string title, string message, string accept);
}
public interface ISalesApi
{
    Task LoginAsync(string email, string password, CancellationToken ct);
    Task<bool> RestoreAsync(CancellationToken ct);
    Task LogoutAsync(CancellationToken ct);
    Task<List<CustomerResponse>> CustomersAsync(CancellationToken ct);
    Task<CustomerResponse> CustomerAsync(Guid id, CancellationToken ct);
    Task<CustomerBalanceResponse> BalanceAsync(Guid id, CancellationToken ct);
    Task<List<SaleResponse>> PurchasesAsync(Guid id, CancellationToken ct);
    Task<List<CityResponse>> CitiesAsync(CancellationToken ct);
    Task<List<StateResponse>> StatesAsync(CancellationToken ct);
    Task<CustomerResponse> CreateCustomerAsync(CustomerRequest request, CancellationToken ct);
    Task<List<ProductResponse>> ProductsAsync(CancellationToken ct);
    Task<List<PaymentTermResponse>> TermsAsync(CancellationToken ct);
    Task<OrderResponse> SubmitAsync(CreateOrderRequest request, CancellationToken ct);
    Task<List<OrderResponse>> OrdersAsync(CancellationToken ct);
    Task<OrderResponse> OrderAsync(Guid id, CancellationToken ct);
}
public sealed class ClientFailure(string message, bool authenticationExpired = false) : Exception(message)
{
    public bool AuthenticationExpired { get; } = authenticationExpired;
}
