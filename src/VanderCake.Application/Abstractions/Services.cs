using VanderCake.Contracts.Api;
using VanderCake.Domain.Catalog;
using VanderCake.Domain.Customers;
using VanderCake.Domain.Finance;
using VanderCake.Domain.Orders;

namespace VanderCake.Application.Abstractions;

public interface ICurrentUser
{
    Guid UserId { get; }
    bool IsAdministrator { get; }
}

public enum FailureKind { NotFound, Forbidden, Conflict }
public sealed class ApplicationFailure(FailureKind kind, string message) : Exception(message)
{
    public FailureKind Kind { get; } = kind;
}

public interface ICustomerService
{
    Task<IReadOnlyList<CustomerResponse>> ListAsync(CancellationToken ct);
    Task<CustomerResponse> GetAsync(Guid id, CancellationToken ct);
    Task<CustomerResponse> CreateAsync(CustomerRequest request, CancellationToken ct);
    Task<CustomerResponse> UpdateAsync(Guid id, UpdateCustomerRequest request, CancellationToken ct);
}

public interface IProductService
{
    Task<IReadOnlyList<ProductResponse>> ListAsync(CancellationToken ct);
    Task<ProductResponse> GetAsync(Guid id, CancellationToken ct);
    Task<ProductResponse> CreateAsync(ProductRequest request, CancellationToken ct);
    Task<ProductResponse> UpdateAsync(Guid id, UpdateProductRequest request, CancellationToken ct);
}

public interface IOrderService
{
    Task<IReadOnlyList<OrderResponse>> ListAsync(CancellationToken ct);
    Task<OrderResponse> GetAsync(Guid id, CancellationToken ct);
    Task<SubmissionResult> SubmitAsync(CreateOrderRequest request, CancellationToken ct);
}

// Focused backend persistence port: no IQueryable or EF types escape Infrastructure.
public interface IDistributionStore
{
    Task<Salesperson?> FindSalespersonForUserAsync(Guid userId, CancellationToken ct);
    Task<Salesperson?> FindSalespersonAsync(Guid id, CancellationToken ct);
    Task<bool> CityExistsAsync(Guid id, CancellationToken ct);
    Task<bool> ProductGroupExistsAsync(Guid id, CancellationToken ct);
    Task<IReadOnlyList<Customer>> CustomersAsync(Guid? ownerId, CancellationToken ct);
    Task<Customer?> CustomerAsync(Guid id, Guid? ownerId, CancellationToken ct);
    Task<IReadOnlyList<Product>> ProductsAsync(bool includeInactive, CancellationToken ct);
    Task<Product?> ProductAsync(Guid id, bool includeInactive, CancellationToken ct);
    Task<PaymentTerm?> PaymentTermAsync(int code, CancellationToken ct);
    Task<IReadOnlyList<Order>> OrdersAsync(Guid? ownerId, CancellationToken ct);
    Task<Order?> OrderAsync(Guid id, Guid? ownerId, CancellationToken ct);
    Task<Order?> OrderBySubmissionAsync(Guid ownerId, Guid key, CancellationToken ct);
    void Add(Customer customer);
    void Add(Product product);
    void Add(Order order);
    Task SaveChangesAsync(CancellationToken ct);
}

public interface IReferenceDataService
{
    Task<IReadOnlyList<StateResponse>> StatesAsync(CancellationToken ct);
    Task<IReadOnlyList<CityResponse>> CitiesAsync(CancellationToken ct);
    Task<IReadOnlyList<ProductGroupResponse>> ProductGroupsAsync(CancellationToken ct);
    Task<IReadOnlyList<PaymentTermResponse>> PaymentTermsAsync(CancellationToken ct);
    Task<IReadOnlyList<SalespersonResponse>> SalespersonsAsync(CancellationToken ct);
}
