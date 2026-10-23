using VanderCake.Application.Abstractions;
using VanderCake.Domain.Customers;

namespace VanderCake.Application.Services;

public sealed class AccessScope(ICurrentUser user, IDistributionStore store)
{
    public bool IsAdministrator => user.IsAdministrator;
    public async Task<Guid?> OwnerIdAsync(CancellationToken ct)
        => user.IsAdministrator ? null : (await SalespersonAsync(ct)).Id;

    public async Task<Salesperson> SalespersonAsync(CancellationToken ct)
    {
        var salesperson = await store.FindSalespersonForUserAsync(user.UserId, ct);
        if (salesperson is null || !salesperson.IsActive)
            throw new ApplicationFailure(FailureKind.Forbidden, "An active salesperson profile is required.");
        return salesperson;
    }

    public void RequireAdministrator()
    {
        if (!user.IsAdministrator)
            throw new ApplicationFailure(FailureKind.Forbidden, "Administrator access is required.");
    }
}
