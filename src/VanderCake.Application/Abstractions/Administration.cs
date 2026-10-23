using VanderCake.Domain.Catalog;
using VanderCake.Domain.Inventory;

namespace VanderCake.Application.Abstractions;

public sealed record DashboardData(DateOnly Date, int PendingOrders, long SalesTodayMinor,
    long OpenPrincipalMinor, long OverduePrincipalMinor, long ExpiringUnits);
public sealed record ManagedSalesperson(Guid Id, string Name, string Email, bool IsActive, Guid Version);
public sealed record ManagedGroup(Guid Id, string Name, Guid Version);
public sealed record SaleSummary(Guid Id, Guid OrderId, string CustomerName, DateOnly Date, long TotalMinor);
public interface IAdministrationService
{
    Task<DashboardData> DashboardAsync(CancellationToken ct);
    Task<IReadOnlyList<ManagedSalesperson>> SalespersonsAsync(CancellationToken ct);
    Task CreateSalespersonAsync(string name, string email, string password, CancellationToken ct);
    Task UpdateSalespersonAsync(Guid id, Guid version, string name, bool active, CancellationToken ct);
    Task<IReadOnlyList<ManagedGroup>> GroupsAsync(CancellationToken ct);
    Task SaveGroupAsync(Guid? id, Guid version, string name, CancellationToken ct);
    Task ReceiveLotAsync(Guid productId, string code, DateOnly received, DateOnly expires, int quantity, CancellationToken ct);
    Task<IReadOnlyList<SaleSummary>> SalesAsync(CancellationToken ct);
}
public interface IAdministrationStore
{
    Task<DashboardData> DashboardAsync(DateOnly today, CancellationToken ct);
    Task<IReadOnlyList<ManagedSalesperson>> SalespersonsAsync(CancellationToken ct);
    Task CreateSalespersonAsync(string name, string email, string password, CancellationToken ct);
    Task UpdateSalespersonAsync(Guid id, Guid version, string name, bool active, CancellationToken ct);
    Task<IReadOnlyList<ManagedGroup>> GroupsAsync(CancellationToken ct);
    Task<ProductGroup?> GroupAsync(Guid id, CancellationToken ct);
    void Add(ProductGroup group);
    void Add(InventoryLot lot);
    Task SaveAsync(CancellationToken ct);
    Task<IReadOnlyList<SaleSummary>> SalesAsync(CancellationToken ct);
}
