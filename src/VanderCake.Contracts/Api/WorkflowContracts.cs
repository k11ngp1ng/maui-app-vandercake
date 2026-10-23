using System.ComponentModel.DataAnnotations;

namespace VanderCake.Contracts.Api;

public sealed record AllocationLineRequest(Guid OrderItemId,
    [property: Range(0, 1_000_000)] int FulfilledQuantity);
public sealed record OrderAllocationRequest(Guid OrderId, Guid Version,
    [property: Required, MinLength(1), MaxLength(100)] List<AllocationLineRequest> Items,
    DateOnly? DispatchDate = null, int? DispatchHour = null);
public sealed record ConfirmAllocationRequest(Guid RequestKey,
    [property: Required, MinLength(1), MaxLength(100)] List<OrderAllocationRequest> Orders);

public sealed record InstallmentSummary(Guid Id, int Number, DateOnly DueDate, long PrincipalMinor);
public sealed record LotMovementResponse(Guid OrderItemId, Guid InventoryLotId, string LotCode,
    DateOnly ExpirationDate, int Quantity, Guid AdministratorId, DateTime ConsumedAtUtc);
public sealed record AllocatedOrderResponse(OrderResponse Order, Guid? SaleId, long SaleTotalMinor,
    IReadOnlyList<InstallmentSummary> Installments, IReadOnlyList<LotMovementResponse> Movements);
public sealed record AllocationResponse(Guid Id, Guid RequestKey, Guid AdministratorId,
    DateTime ConfirmedAtUtc, IReadOnlyList<AllocatedOrderResponse> Orders);
public sealed record AllocationResult(AllocationResponse Allocation, bool Created);

public sealed record StockLotResponse(Guid Id, string Code, DateOnly ReceivedDate, DateOnly ExpirationDate,
    int AvailableQuantity, bool Eligible, Guid Version);
public sealed record StockResponse(Guid ProductId, string ProductName, DateOnly DispatchDate,
    long PhysicalQuantity, long EligibleQuantity, long ExpiredQuantity, IReadOnlyList<StockLotResponse> Lots);

public sealed record RegisterPaymentRequest(Guid RequestKey, Guid Version, DateOnly QuoteDate,
    [property: Range(0, long.MaxValue)] long AmountMinor, bool ConfirmZero = false);
public sealed record PaymentResponse(Guid Id, Guid AccountsReceivableId, Guid RequestKey, DateOnly PaymentDate,
    long PrincipalMinor, long PenaltyMinor, long InterestMinor, long DiscountMinor, long PaidMinor,
    Guid AdministratorId, DateTime RecordedAtUtc);
public sealed record PaymentResult(PaymentResponse Payment, bool Created);
public sealed record ReceivableResponse(Guid Id, Guid SaleId, Guid CustomerId, string CustomerName,
    int Number, DateOnly DueDate, string Status, DateOnly QuoteDate, long PrincipalMinor,
    long PayableMinor, long PenaltyMinor, long InterestMinor, long DiscountMinor,
    int PenaltyBasisPoints, int DailyInterestBasisPoints, int DailyDiscountBasisPoints,
    Guid Version, PaymentResponse? Payment);
public sealed record CustomerBalanceResponse(Guid CustomerId, DateOnly AsOfDate, long OpenPrincipalMinor,
    long OverduePrincipalMinor, long PayableMinor, IReadOnlyList<ReceivableResponse> Installments);
public sealed record SaleItemResponse(Guid ProductId, string ProductName, int Quantity, long UnitPriceMinor, long TotalMinor);
public sealed record SaleResponse(Guid Id, Guid OrderId, Guid CustomerId, string CustomerName, DateOnly SaleDate,
    int PaymentTermCode, long TotalMinor, IReadOnlyList<SaleItemResponse> Items,
    IReadOnlyList<ReceivableResponse> Installments);
public sealed record FinanceRulesResponse(int PenaltyBasisPoints, int DailyInterestBasisPoints, int DailyDiscountBasisPoints);
