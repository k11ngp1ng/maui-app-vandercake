using System.Data;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using VanderCake.Application.Abstractions;
using VanderCake.Domain.Catalog;
using VanderCake.Domain.Finance;
using VanderCake.Domain.Inventory;
using VanderCake.Domain.Orders;

namespace VanderCake.Infrastructure.Persistence;

// One async gate per server process; SQLite still enforces the real database transaction.
public sealed class WorkflowWriteCoordinator : IDisposable
{
    internal SemaphoreSlim Gate { get; } = new(1, 1);
    public void Dispose() => Gate.Dispose();
}

public sealed class WorkflowStore(VanderCakeDbContext db, WorkflowWriteCoordinator coordinator) : IWorkflowStore
{
    public async Task<IWorkflowTransaction> BeginWriteAsync(CancellationToken ct)
    {
        await coordinator.Gate.WaitAsync(ct);
        try
        {
            // Authentication/read-only work may have populated this scoped context beforehand.
            db.ChangeTracker.Clear();
            ((SqliteConnection)db.Database.GetDbConnection()).DefaultTimeout = 5;
            // Microsoft.Data.Sqlite's non-deferred Serializable transaction acquires the writer reservation.
            var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
            return new Lease(db, transaction, coordinator.Gate);
        }
        catch { coordinator.Gate.Release(); throw; }
    }

    private sealed class Lease(VanderCakeDbContext db, IDbContextTransaction transaction, SemaphoreSlim gate) : IWorkflowTransaction
    {
        public Task CommitAsync(CancellationToken ct) => transaction.CommitAsync(ct);
        public async ValueTask DisposeAsync()
        {
            try { await transaction.DisposeAsync(); }
            finally { db.ChangeTracker.Clear(); gate.Release(); }
        }
    }

    public async Task<IReadOnlyList<Order>> SubmittedAsync(CancellationToken ct)
        => await db.Orders.AsNoTracking().Include(o => o.Items).Where(o => o.Status == OrderStatus.Submitted)
            .OrderBy(o => o.DispatchDate).ThenBy(o => o.SubmittedAtUtc).ToListAsync(ct);
    public async Task<IReadOnlyList<InventoryLot>> LotsAsync(IReadOnlyCollection<Guid> productIds, CancellationToken ct)
        => await db.InventoryLots.Where(l => productIds.Contains(l.ProductId)).ToListAsync(ct);
    public async Task<(IReadOnlyList<Product> Products, IReadOnlyList<InventoryLot> Lots)> StockAsync(CancellationToken ct)
        => (await db.Products.AsNoTracking().OrderBy(p => p.Name).ToListAsync(ct),
            await db.InventoryLots.AsNoTracking().ToListAsync(ct));
    public Task<AllocationBatch?> BatchByRequestAsync(Guid requestKey, CancellationToken ct)
        => db.AllocationBatches.AsNoTracking().SingleOrDefaultAsync(b => b.RequestKey == requestKey, ct);

    public async Task<AllocationData?> BatchAsync(Guid batchId, CancellationToken ct)
    {
        var batch = await db.AllocationBatches.AsNoTracking().SingleOrDefaultAsync(b => b.Id == batchId, ct);
        if (batch is null) return null;
        var orders = await db.Orders.AsNoTracking().Include(o => o.Items)
            .Where(o => o.AllocationBatchId == batchId).OrderBy(o => o.Id).ToListAsync(ct);
        var orderIds = orders.Select(o => o.Id).ToArray();
        var itemIds = orders.SelectMany(o => o.Items).Select(i => i.Id).ToArray();
        var sales = await db.Sales.AsNoTracking().Where(s => orderIds.Contains(s.OrderId)).ToListAsync(ct);
        var saleIds = sales.Select(s => s.Id).ToArray();
        var installments = await db.AccountsReceivable.AsNoTracking().Where(a => saleIds.Contains(a.SaleId)).ToListAsync(ct);
        var movements = await (from movement in db.LotConsumptions.AsNoTracking()
            join lot in db.InventoryLots.AsNoTracking() on movement.InventoryLotId equals lot.Id
            where itemIds.Contains(movement.OrderItemId)
            select new LotMovementData(movement, lot)).ToListAsync(ct);
        return new(batch, orders, sales, installments, movements);
    }

    private IQueryable<Sale> Sales(Guid? owner)
        => db.Sales.Where(s => owner == null || db.Customers.Any(c => c.Id == s.CustomerId && c.SalespersonId == owner));
    private IQueryable<ReceivableData> Accounts(Guid? owner, Guid? customerId = null, Guid? accountId = null, Guid? saleId = null)
        => from account in db.AccountsReceivable.Where(a => accountId == null || a.Id == accountId)
           join sale in Sales(owner).Where(s => (customerId == null || s.CustomerId == customerId)
               && (saleId == null || s.Id == saleId)) on account.SaleId equals sale.Id
           join payment in db.Payments on account.Id equals payment.AccountsReceivableId into payments
           from payment in payments.DefaultIfEmpty()
           orderby account.DueDate, account.Number, account.Id
           select new ReceivableData(account, sale, payment);

    public Task<PaymentRecord?> PaymentByRequestAsync(Guid requestKey, CancellationToken ct)
        => db.Payments.AsNoTracking().SingleOrDefaultAsync(p => p.RequestKey == requestKey, ct);
    public Task<PaymentRecord?> PaymentAsync(Guid id, Guid? ownerId, CancellationToken ct)
        => (from payment in db.Payments.AsNoTracking()
            join account in db.AccountsReceivable on payment.AccountsReceivableId equals account.Id
            join sale in Sales(ownerId) on account.SaleId equals sale.Id
            where payment.Id == id
            select payment).SingleOrDefaultAsync(ct);
    public Task<ReceivableData?> ReceivableAsync(Guid id, Guid? ownerId, bool tracking, CancellationToken ct)
        => (tracking ? Accounts(ownerId, accountId: id) : Accounts(ownerId, accountId: id).AsNoTracking()).SingleOrDefaultAsync(ct);
    public async Task<IReadOnlyList<ReceivableData>> ReceivablesAsync(Guid? customerId, Guid? ownerId, CancellationToken ct)
        => await Accounts(ownerId, customerId: customerId).AsNoTracking().ToListAsync(ct);
    public async Task<SaleData?> SaleAsync(Guid id, Guid? ownerId, CancellationToken ct)
    {
        var sale = await Sales(ownerId).AsNoTracking().Include(s => s.Items).SingleOrDefaultAsync(s => s.Id == id, ct);
        if (sale is null) return null;
        return new(sale, await Accounts(ownerId, saleId: id).AsNoTracking().ToListAsync(ct));
    }
    public async Task<IReadOnlyList<SaleData>> PurchasesAsync(Guid customerId, Guid? ownerId, CancellationToken ct)
    {
        var sales = await Sales(ownerId).AsNoTracking().Include(s => s.Items).Where(s => s.CustomerId == customerId)
            .OrderByDescending(s => s.SaleDate).ThenBy(s => s.Id).ToListAsync(ct);
        var accounts = await ReceivablesAsync(customerId, ownerId, ct);
        return sales.Select(s => new SaleData(s, accounts.Where(a => a.Sale.Id == s.Id).OrderBy(a => a.Account.Number).ToList())).ToList();
    }
    public void Add(AllocationBatch batch) => db.AllocationBatches.Add(batch);
    public void Add(LotConsumption movement) => db.LotConsumptions.Add(movement);
    public void Add(Sale sale) => db.Sales.Add(sale);
    public void Add(AccountsReceivable account) => db.AccountsReceivable.Add(account);
    public void Add(PaymentRecord payment) => db.Payments.Add(payment);
    public async Task SaveChangesAsync(CancellationToken ct) => await db.SaveChangesAsync(ct);
}
