using Microsoft.EntityFrameworkCore;
using VanderCake.Application.Abstractions;
using VanderCake.Domain.Catalog;
using VanderCake.Domain.Customers;
using VanderCake.Domain.Finance;
using VanderCake.Domain.Orders;

namespace VanderCake.Infrastructure.Persistence;

public sealed class DistributionStore(VanderCakeDbContext db) : IDistributionStore
{
    public Task<Salesperson?> FindSalespersonForUserAsync(Guid userId, CancellationToken ct)
        => db.Salespersons.SingleOrDefaultAsync(x => x.UserId == userId, ct);
    public Task<Salesperson?> FindSalespersonAsync(Guid id, CancellationToken ct)
        => db.Salespersons.SingleOrDefaultAsync(x => x.Id == id, ct);
    public Task<bool> CityExistsAsync(Guid id, CancellationToken ct) => db.Cities.AnyAsync(x => x.Id == id, ct);
    public Task<bool> ProductGroupExistsAsync(Guid id, CancellationToken ct) => db.ProductGroups.AnyAsync(x => x.Id == id, ct);
    private IQueryable<Customer> Customers(Guid? owner) => db.Customers.Where(x => owner == null || x.SalespersonId == owner);
    public async Task<IReadOnlyList<Customer>> CustomersAsync(Guid? ownerId, CancellationToken ct)
        => await Customers(ownerId).OrderBy(x => x.VisitOrder).ThenBy(x => x.Id).Take(500).ToListAsync(ct);
    public Task<Customer?> CustomerAsync(Guid id, Guid? ownerId, CancellationToken ct)
        => Customers(ownerId).SingleOrDefaultAsync(x => x.Id == id, ct);
    public async Task<IReadOnlyList<Product>> ProductsAsync(bool includeInactive, CancellationToken ct)
        => await db.Products.Where(x => includeInactive || x.IsActive).OrderBy(x => x.Name).Take(500).ToListAsync(ct);
    public Task<Product?> ProductAsync(Guid id, bool includeInactive, CancellationToken ct)
        => db.Products.SingleOrDefaultAsync(x => x.Id == id && (includeInactive || x.IsActive), ct);
    public Task<PaymentTerm?> PaymentTermAsync(int code, CancellationToken ct)
        => db.PaymentTerms.Include(x => x.Definitions).SingleOrDefaultAsync(x => x.Code == code, ct);
    private IQueryable<Order> Orders(Guid? owner)
        => db.Orders.Include(x => x.Items).Where(x => owner == null ||
            (x.SalespersonId == owner && db.Customers.Any(c => c.Id == x.CustomerId && c.SalespersonId == owner)));
    public async Task<IReadOnlyList<Order>> OrdersAsync(Guid? ownerId, CancellationToken ct)
        => await Orders(ownerId).OrderByDescending(x => x.SubmittedAtUtc).Take(500).ToListAsync(ct);
    public Task<Order?> OrderAsync(Guid id, Guid? ownerId, CancellationToken ct)
        => Orders(ownerId).SingleOrDefaultAsync(x => x.Id == id, ct);
    public Task<Order?> OrderBySubmissionAsync(Guid ownerId, Guid key, CancellationToken ct)
        => Orders(ownerId).SingleOrDefaultAsync(x => x.SubmissionKey == key, ct);
    public void Add(Customer customer) => db.Customers.Add(customer);
    public void Add(Product product) => db.Products.Add(product);
    public void Add(Order order) => db.Orders.Add(order);
    public async Task SaveChangesAsync(CancellationToken ct) => await db.SaveChangesAsync(ct);
}
