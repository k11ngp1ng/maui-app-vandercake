using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using VanderCake.Application.Abstractions;
using VanderCake.Contracts.Api;
using VanderCake.Domain.Common;
using VanderCake.Domain.Finance;
using VanderCake.Domain.Inventory;
using VanderCake.Domain.Orders;

namespace VanderCake.Application.Services;

public sealed class FulfillmentService(IWorkflowStore workflow, IDistributionStore data, AccessScope access,
    ICurrentUser user, BusinessClock clock, FinancialRules rules) : IFulfillmentService
{
    public async Task<IReadOnlyList<OrderResponse>> SubmittedAsync(CancellationToken ct)
    {
        access.RequireAdministrator();
        return (await workflow.SubmittedAsync(ct)).Select(OrderService.Map).ToList();
    }

    public async Task<IReadOnlyList<StockResponse>> StockAsync(DateOnly? dispatchDate, CancellationToken ct)
    {
        access.RequireAdministrator();
        var today = clock.Today;
        var date = Rule.Date(dispatchDate ?? today, nameof(dispatchDate));
        if (date < today) throw new DomainRuleException("Stock planning date cannot be in the past.");
        var (products, lots) = await workflow.StockAsync(ct);
        return products.Select(p =>
        {
            var productLots = lots.Where(l => l.ProductId == p.Id).OrderBy(l => l.ExpirationDate).ThenBy(l => l.Id).ToList();
            return new StockResponse(p.Id, p.Name, date, productLots.Sum(l => (long)l.AvailableQuantity),
                productLots.Where(l => l.IsEligible(date, today)).Sum(l => (long)l.AvailableQuantity),
                productLots.Where(l => l.ExpirationDate < today).Sum(l => (long)l.AvailableQuantity),
                productLots.Select(l => new StockLotResponse(l.Id, l.Code, l.ReceivedDate,
                    l.ExpirationDate, l.AvailableQuantity, l.IsEligible(date, today), l.Version)).ToList());
        }).ToList();
    }

    public async Task<AllocationResult> ConfirmAsync(ConfirmAllocationRequest request, CancellationToken ct)
    {
        access.RequireAdministrator();
        Rule.Id(request.RequestKey, nameof(request.RequestKey));
        if (request.Orders is null || request.Orders.Count is < 1 or > 100 || request.Orders.Any(o => o is null)
            || request.Orders.Select(o => o.OrderId).Distinct().Count() != request.Orders.Count)
            throw new DomainRuleException("Select 1 to 100 distinct orders.");
        foreach (var selected in request.Orders)
        {
            Rule.Id(selected.OrderId, nameof(selected.OrderId)); Rule.Id(selected.Version, nameof(selected.Version));
            if (selected.Items is null || selected.Items.Count is < 1 or > 100 || selected.Items.Any(i => i is null)
                || selected.Items.Select(i => i.OrderItemId).Distinct().Count() != selected.Items.Count)
                throw new DomainRuleException("Provide every order line exactly once.");
            if (selected.Items.Any(i => i.FulfilledQuantity is < 0 or > 1_000_000))
                throw new DomainRuleException("Approved quantities must be between zero and 1000000.");
            if (selected.DispatchDate.HasValue != selected.DispatchHour.HasValue)
                throw new DomainRuleException("Supply both dispatch date and hour when rescheduling.");
        }
        var fingerprint = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(
            request.Orders.OrderBy(o => o.OrderId).Select(o => new { o.OrderId, o.Version, o.DispatchDate, o.DispatchHour,
                Items = o.Items.OrderBy(i => i.OrderItemId).ToArray() })))));
        await using var transaction = await workflow.BeginWriteAsync(ct);
        var existing = await workflow.BatchByRequestAsync(request.RequestKey, ct);
        if (existing is not null)
        {
            if (existing.RequestFingerprint != fingerprint)
                throw new ApplicationFailure(FailureKind.Conflict, "Allocation key was already used for different content.");
            return new(Map((await workflow.BatchAsync(existing.Id, ct))!), false);
        }

        var now = clock.UtcNow;
        var today = DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(now, BusinessClock.Zone).DateTime);
        var batch = new AllocationBatch(request.RequestKey, fingerprint, user.UserId, now.UtcDateTime);
        var orders = new List<Order>();
        foreach (var selected in request.Orders)
        {
            var order = await data.OrderAsync(selected.OrderId, null, ct)
                ?? throw new ApplicationFailure(FailureKind.NotFound, "Order was not found.");
            if (order.Status != OrderStatus.Submitted)
                throw new ApplicationFailure(FailureKind.Conflict, "Order is already finalized.");
            if (order.Version != selected.Version)
                throw new ApplicationFailure(FailureKind.Conflict, "Order changed; reload before allocating.");
            if (selected.DispatchDate is { } date)
                order.Reschedule(date, (DispatchWindow)selected.DispatchHour!.Value, now, BusinessClock.Zone);
            order.FinalizeAllocation(batch.Id, selected.Items.ToDictionary(i => i.OrderItemId, i => i.FulfilledQuantity),
                now, BusinessClock.Zone);
            orders.Add(order);
        }
        var productIds = orders.SelectMany(o => o.Items).Select(i => i.ProductId).Distinct().ToArray();
        var lots = await workflow.LotsAsync(productIds, ct);
        workflow.Add(batch);
        foreach (var order in orders.OrderBy(o => o.DispatchDate).ThenBy(o => o.Id))
            foreach (var item in order.Items)
                foreach (var draw in Fefo.Consume(lots.Where(l => l.ProductId == item.ProductId),
                    item.FulfilledQuantity, order.DispatchDate, today))
                    workflow.Add(new LotConsumption(item.Id, draw.Lot.Id, draw.Quantity, user.UserId, now.UtcDateTime));

        // First flush proves that subsequent finance failure still rolls inventory/order writes back.
        await workflow.SaveChangesAsync(ct);
        foreach (var order in orders.Where(o => o.Status != OrderStatus.Cut))
        {
            var sale = new Sale(order, today);
            var term = await data.PaymentTermAsync(order.PaymentTermCode, ct)
                ?? throw new DomainRuleException("Payment term is unavailable.");
            workflow.Add(sale);
            foreach (var installment in term.Schedule(sale.TotalMinor, today))
                workflow.Add(new AccountsReceivable(sale.Id, installment, today, rules));
        }
        await workflow.SaveChangesAsync(ct);
        var result = Map((await workflow.BatchAsync(batch.Id, ct))!);
        await transaction.CommitAsync(ct);
        return new(result, true);
    }

    public async Task<AllocationResponse> GetAsync(Guid batchId, CancellationToken ct)
    {
        access.RequireAdministrator();
        return Map(await workflow.BatchAsync(batchId, ct)
            ?? throw new ApplicationFailure(FailureKind.NotFound, "Allocation was not found."));
    }

    private static AllocationResponse Map(AllocationData d) => new(d.Batch.Id, d.Batch.RequestKey,
        d.Batch.AdministratorId, DateTime.SpecifyKind(d.Batch.ConfirmedAtUtc, DateTimeKind.Utc),
        d.Orders.Select(o =>
        {
            var sale = d.Sales.SingleOrDefault(s => s.OrderId == o.Id);
            var itemIds = o.Items.Select(i => i.Id).ToHashSet();
            return new AllocatedOrderResponse(OrderService.Map(o), sale?.Id, sale?.TotalMinor ?? 0,
                d.Installments.Where(a => a.SaleId == sale?.Id).OrderBy(a => a.Number)
                    .Select(a => new InstallmentSummary(a.Id, a.Number, a.DueDate, a.PrincipalMinor)).ToList(),
                d.Movements.Where(m => itemIds.Contains(m.Movement.OrderItemId)).OrderBy(m => m.Lot.ExpirationDate)
                    .ThenBy(m => m.Lot.Id).Select(m => new LotMovementResponse(m.Movement.OrderItemId,
                        m.Lot.Id, m.Lot.Code, m.Lot.ExpirationDate, m.Movement.Quantity,
                        m.Movement.AdministratorId, DateTime.SpecifyKind(m.Movement.ConsumedAtUtc, DateTimeKind.Utc))).ToList());
        }).ToList());
}
