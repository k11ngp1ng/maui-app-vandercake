using VanderCake.Application.Abstractions;
using VanderCake.Domain.Catalog;
using VanderCake.Domain.Common;
using VanderCake.Domain.Inventory;

namespace VanderCake.Application.Services;

public sealed class AdministrationService(IAdministrationStore store, IDistributionStore data,
    AccessScope access, BusinessClock clock) : IAdministrationService
{
    public Task<DashboardData> DashboardAsync(CancellationToken ct)
    { access.RequireAdministrator(); return store.DashboardAsync(clock.Today, ct); }
    public Task<IReadOnlyList<ManagedSalesperson>> SalespersonsAsync(CancellationToken ct)
    { access.RequireAdministrator(); return store.SalespersonsAsync(ct); }
    public Task CreateSalespersonAsync(string name, string email, string password, CancellationToken ct)
    {
        access.RequireAdministrator();
        return store.CreateSalespersonAsync(Rule.Text(name, nameof(name)), email.Trim(), password, ct);
    }
    public Task UpdateSalespersonAsync(Guid id, Guid version, string name, bool active, CancellationToken ct)
    {
        access.RequireAdministrator();
        return store.UpdateSalespersonAsync(id, version, Rule.Text(name, nameof(name)), active, ct);
    }
    public Task<IReadOnlyList<ManagedGroup>> GroupsAsync(CancellationToken ct)
    { access.RequireAdministrator(); return store.GroupsAsync(ct); }
    public async Task SaveGroupAsync(Guid? id, Guid version, string name, CancellationToken ct)
    {
        access.RequireAdministrator();
        if (id is { } groupId)
        {
            var group = await store.GroupAsync(groupId, ct)
                ?? throw new ApplicationFailure(FailureKind.NotFound, "Product group was not found.");
            if (group.Version != version) throw new ApplicationFailure(FailureKind.Conflict, "Group changed; reload before editing.");
            group.Rename(name);
        }
        else store.Add(new ProductGroup(name));
        await store.SaveAsync(ct);
    }
    public async Task ReceiveLotAsync(Guid productId, string code, DateOnly received, DateOnly expires, int quantity, CancellationToken ct)
    {
        access.RequireAdministrator();
        var product = await data.ProductAsync(productId, true, ct);
        if (product is null || !product.IsActive) throw new DomainRuleException("Choose an active product.");
        if (received > clock.Today) throw new DomainRuleException("Stock receipt cannot be in the future.");
        if (expires < clock.Today) throw new DomainRuleException("Cannot receive expired stock.");
        store.Add(new InventoryLot(productId, code, received, expires, quantity));
        await store.SaveAsync(ct);
    }
    public Task<IReadOnlyList<SaleSummary>> SalesAsync(CancellationToken ct)
    { access.RequireAdministrator(); return store.SalesAsync(ct); }
}
