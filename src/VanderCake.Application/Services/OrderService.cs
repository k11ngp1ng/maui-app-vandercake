using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using VanderCake.Application.Abstractions;
using VanderCake.Contracts.Api;
using VanderCake.Domain.Common;
using VanderCake.Domain.Orders;

namespace VanderCake.Application.Services;

public sealed class OrderService(IDistributionStore store, AccessScope access, TimeProvider time) : IOrderService
{
    private static readonly TimeZoneInfo BusinessTimeZone = TimeZoneInfo.FindSystemTimeZoneById("America/Sao_Paulo");

    public async Task<IReadOnlyList<OrderResponse>> ListAsync(CancellationToken ct)
        => (await store.OrdersAsync(await access.OwnerIdAsync(ct), ct)).Select(Map).ToList();

    public async Task<OrderResponse> GetAsync(Guid id, CancellationToken ct)
        => Map(await store.OrderAsync(id, await access.OwnerIdAsync(ct), ct)
            ?? throw new ApplicationFailure(FailureKind.NotFound, "Order was not found."));

    public async Task<SubmissionResult> SubmitAsync(CreateOrderRequest request, CancellationToken ct)
    {
        var salesperson = await access.SalespersonAsync(ct);
        var customer = await store.CustomerAsync(request.CustomerId, salesperson.Id, ct)
            ?? throw new ApplicationFailure(FailureKind.NotFound, "Customer was not found.");
        if (!customer.IsActive) throw new DomainRuleException("Customer is inactive.");
        if (request.Items is null || request.Items.Count is < 1 or > 100 || request.Items.Any(x => x is null)
            || request.Items.Select(x => x.ProductId).Distinct().Count() != request.Items.Count)
            throw new DomainRuleException("Provide 1 to 100 distinct product lines.");
        Rule.Id(request.SubmissionKey, nameof(request.SubmissionKey));
        var canonical = new { request.CustomerId, request.PaymentTermCode, request.DispatchDate,
            request.DispatchHour, Items = request.Items.OrderBy(x => x.ProductId).ToArray() };
        var fingerprint = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(canonical))));
        var existing = await store.OrderBySubmissionAsync(salesperson.Id, request.SubmissionKey, ct);
        if (existing is not null)
        {
            if (existing.RequestFingerprint != fingerprint)
                throw new ApplicationFailure(FailureKind.Conflict, "Submission key was already used for different content.");
            return new(Map(existing), false);
        }
        if (await store.PaymentTermAsync(request.PaymentTermCode, ct) is null)
            throw new DomainRuleException("Select an existing payment term.");
        var order = new Order(customer.Id, salesperson.Id, customer.Name, salesperson.Name,
            request.PaymentTermCode, request.DispatchDate, (DispatchWindow)request.DispatchHour,
            time.GetUtcNow(), BusinessTimeZone, request.SubmissionKey, fingerprint);
        foreach (var line in request.Items)
        {
            var product = await store.ProductAsync(line.ProductId, false, ct)
                ?? throw new DomainRuleException("Order contains an unknown or inactive product.");
            order.AddItem(product.Id, product.Name, line.Quantity, product.PriceMinor);
        }
        store.Add(order); await store.SaveChangesAsync(ct);
        return new(Map(order), true);
    }

    public static OrderResponse Map(Order o) => new(o.Id, o.CustomerId, o.SalespersonId,
        o.CustomerName, o.PaymentTermCode, o.DispatchDate, (int)o.DispatchWindow, o.Status.ToString(),
        DateTime.SpecifyKind(o.SubmittedAtUtc, DateTimeKind.Utc), o.RequestedTotalMinor, o.Version,
        o.Items.OrderBy(x => x.ProductId).Select(i => new OrderItemResponse(i.Id, i.ProductId, i.ProductName,
            i.RequestedQuantity, i.FulfilledQuantity, i.RequestedQuantity - i.FulfilledQuantity, i.UnitPriceMinor)).ToList());
}
