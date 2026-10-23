using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using VanderCake.Application.Abstractions;
using VanderCake.Domain.Catalog;
using VanderCake.Domain.Common;
using VanderCake.Domain.Customers;
using VanderCake.Domain.Finance;
using VanderCake.Domain.Inventory;
using VanderCake.Domain.Orders;
using VanderCake.Infrastructure.Identity;

namespace VanderCake.Infrastructure.Persistence;

public sealed class AdministrationStore(VanderCakeDbContext db, UserManager<ApplicationUser> users) : IAdministrationStore
{
    public async Task<DashboardData> DashboardAsync(DateOnly today, CancellationToken ct)
    {
        var horizon = today.AddDays(7);
        return new(today, await db.Orders.CountAsync(x => x.Status == OrderStatus.Submitted, ct),
            await db.Sales.Where(x => x.SaleDate == today).SumAsync(x => (long?)x.TotalMinor, ct) ?? 0,
            await db.AccountsReceivable.Where(x => x.Status == FinancialStatus.Open).SumAsync(x => (long?)x.PrincipalMinor, ct) ?? 0,
            await db.AccountsReceivable.Where(x => x.Status == FinancialStatus.Open && x.DueDate < today).SumAsync(x => (long?)x.PrincipalMinor, ct) ?? 0,
            await db.InventoryLots.Where(x => x.ReceivedDate <= today && x.ExpirationDate >= today && x.ExpirationDate <= horizon)
                .SumAsync(x => (long?)x.AvailableQuantity, ct) ?? 0);
    }
    public async Task<IReadOnlyList<ManagedSalesperson>> SalespersonsAsync(CancellationToken ct)
        => await (from s in db.Salespersons.AsNoTracking() join u in db.Users on s.UserId equals u.Id
                  orderby s.Name select new ManagedSalesperson(s.Id, s.Name, u.Email!, s.IsActive, s.Version)).ToListAsync(ct);
    private static void Check(IdentityResult result)
    { if (!result.Succeeded) throw new DomainRuleException(string.Join(" ", result.Errors.Select(e => e.Description))); }
    public async Task CreateSalespersonAsync(string name, string email, string password, CancellationToken ct)
    {
        await using var transaction = await db.Database.BeginTransactionAsync(ct);
        var user = new ApplicationUser { Id = Guid.NewGuid(), UserName = email, Email = email, IsActive = true };
        Check(await users.CreateAsync(user, password));
        Check(await users.AddToRoleAsync(user, RoleNames.Salesperson));
        db.Salespersons.Add(new Salesperson(user.Id, name));
        await db.SaveChangesAsync(ct); await transaction.CommitAsync(ct);
    }
    public async Task UpdateSalespersonAsync(Guid id, Guid version, string name, bool active, CancellationToken ct)
    {
        await using var transaction = await db.Database.BeginTransactionAsync(ct);
        var seller = await db.Salespersons.SingleOrDefaultAsync(x => x.Id == id, ct)
            ?? throw new ApplicationFailure(FailureKind.NotFound, "Salesperson was not found.");
        if (seller.Version != version) throw new ApplicationFailure(FailureKind.Conflict, "Salesperson changed; reload before editing.");
        seller.Update(name, active);
        var user = await users.FindByIdAsync(seller.UserId.ToString())
            ?? throw new ApplicationFailure(FailureKind.NotFound, "User was not found.");
        user.IsActive = active; Check(await users.UpdateAsync(user));
        Check(await users.UpdateSecurityStampAsync(user));
        await db.SaveChangesAsync(ct); await transaction.CommitAsync(ct);
    }
    public async Task<IReadOnlyList<ManagedGroup>> GroupsAsync(CancellationToken ct)
        => await db.ProductGroups.AsNoTracking().OrderBy(x => x.Name).Select(x => new ManagedGroup(x.Id, x.Name, x.Version)).ToListAsync(ct);
    public Task<ProductGroup?> GroupAsync(Guid id, CancellationToken ct) => db.ProductGroups.SingleOrDefaultAsync(x => x.Id == id, ct);
    public void Add(ProductGroup group) => db.ProductGroups.Add(group);
    public void Add(InventoryLot lot) => db.InventoryLots.Add(lot);
    public Task SaveAsync(CancellationToken ct) => db.SaveChangesAsync(ct);
    public async Task<IReadOnlyList<SaleSummary>> SalesAsync(CancellationToken ct)
        => await db.Sales.AsNoTracking().OrderByDescending(x => x.SaleDate).ThenBy(x => x.Id)
            .Select(x => new SaleSummary(x.Id, x.OrderId, x.CustomerName, x.SaleDate, x.TotalMinor)).ToListAsync(ct);
}
