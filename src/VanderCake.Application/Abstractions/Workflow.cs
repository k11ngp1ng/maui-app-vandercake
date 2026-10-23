using VanderCake.Contracts.Api;
using VanderCake.Domain.Catalog;
using VanderCake.Domain.Finance;
using VanderCake.Domain.Inventory;
using VanderCake.Domain.Orders;

namespace VanderCake.Application.Abstractions;

public interface IFulfillmentService
{
    Task<IReadOnlyList<OrderResponse>> SubmittedAsync(CancellationToken ct);
    Task<IReadOnlyList<StockResponse>> StockAsync(DateOnly? dispatchDate, CancellationToken ct);
    Task<AllocationResult> ConfirmAsync(ConfirmAllocationRequest request, CancellationToken ct);
    Task<AllocationResponse> GetAsync(Guid batchId, CancellationToken ct);
}

public interface IFinanceService
{
    Task<CustomerBalanceResponse> BalanceAsync(Guid customerId, CancellationToken ct);
    Task<IReadOnlyList<SaleResponse>> PurchasesAsync(Guid customerId, CancellationToken ct);
    Task<SaleResponse> SaleAsync(Guid id, CancellationToken ct);
    Task<IReadOnlyList<ReceivableResponse>> ReceivablesAsync(Guid? customerId, CancellationToken ct);
    Task<ReceivableResponse> ReceivableAsync(Guid id, CancellationToken ct);
    Task<PaymentResult> PayAsync(Guid receivableId, RegisterPaymentRequest request, CancellationToken ct);
    Task<PaymentResponse> PaymentAsync(Guid id, CancellationToken ct);
}

public interface IWorkflowTransaction : IAsyncDisposable
{
    Task CommitAsync(CancellationToken ct);
}

public sealed record LotMovementData(LotConsumption Movement, InventoryLot Lot);
public sealed record AllocationData(AllocationBatch Batch, IReadOnlyList<Order> Orders,
    IReadOnlyList<Sale> Sales, IReadOnlyList<AccountsReceivable> Installments, IReadOnlyList<LotMovementData> Movements);
public sealed record ReceivableData(AccountsReceivable Account, Sale Sale, PaymentRecord? Payment);
public sealed record SaleData(Sale Sale, IReadOnlyList<ReceivableData> Installments);

public interface IWorkflowStore
{
    Task<IWorkflowTransaction> BeginWriteAsync(CancellationToken ct);
    Task<IReadOnlyList<Order>> SubmittedAsync(CancellationToken ct);
    Task<IReadOnlyList<InventoryLot>> LotsAsync(IReadOnlyCollection<Guid> productIds, CancellationToken ct);
    Task<(IReadOnlyList<Product> Products, IReadOnlyList<InventoryLot> Lots)> StockAsync(CancellationToken ct);
    Task<AllocationBatch?> BatchByRequestAsync(Guid requestKey, CancellationToken ct);
    Task<AllocationData?> BatchAsync(Guid batchId, CancellationToken ct);
    Task<PaymentRecord?> PaymentByRequestAsync(Guid requestKey, CancellationToken ct);
    Task<PaymentRecord?> PaymentAsync(Guid id, Guid? ownerId, CancellationToken ct);
    Task<ReceivableData?> ReceivableAsync(Guid id, Guid? ownerId, bool tracking, CancellationToken ct);
    Task<IReadOnlyList<ReceivableData>> ReceivablesAsync(Guid? customerId, Guid? ownerId, CancellationToken ct);
    Task<SaleData?> SaleAsync(Guid id, Guid? ownerId, CancellationToken ct);
    Task<IReadOnlyList<SaleData>> PurchasesAsync(Guid customerId, Guid? ownerId, CancellationToken ct);
    void Add(AllocationBatch batch);
    void Add(LotConsumption movement);
    void Add(Sale sale);
    void Add(AccountsReceivable account);
    void Add(PaymentRecord payment);
    Task SaveChangesAsync(CancellationToken ct);
}
