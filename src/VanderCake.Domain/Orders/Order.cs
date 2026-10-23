using VanderCake.Domain.Common;

namespace VanderCake.Domain.Orders;

public enum OrderStatus { Submitted = 0, Fulfilled = 1, PartiallyFulfilled = 2, Cut = 3 }
public enum DispatchWindow { Noon = 12, Evening = 19 }

public sealed class Order : Entity
{
    private readonly List<OrderItem> _items = [];
    private Order() { }
    public Order(Guid customerId, Guid salespersonId, string customerName, string salespersonName,
        int paymentTermCode, DateOnly dispatchDate, DispatchWindow window,
        DateTimeOffset now, TimeZoneInfo businessTimeZone, Guid submissionKey, string requestFingerprint)
    {
        CustomerId = Rule.Id(customerId, nameof(CustomerId));
        SalespersonId = Rule.Id(salespersonId, nameof(SalespersonId));
        CustomerName = Rule.Text(customerName, nameof(CustomerName));
        SalespersonName = Rule.Text(salespersonName, nameof(SalespersonName));
        if (paymentTermCode is not (10 or 20)) throw new DomainRuleException("Unknown payment term.");
        if (!Enum.IsDefined(window)) throw new DomainRuleException("Dispatch window must be 12:00 or 19:00.");
        Rule.Date(dispatchDate, nameof(dispatchDate));
        var localNow = TimeZoneInfo.ConvertTime(now, businessTimeZone).DateTime;
        if (dispatchDate.ToDateTime(new TimeOnly((int)window, 0)) <= localNow)
            throw new DomainRuleException("Dispatch must be in the future.");
        PaymentTermCode = paymentTermCode; DispatchDate = dispatchDate; DispatchWindow = window;
        SubmittedAtUtc = now.UtcDateTime; SubmissionKey = Rule.Id(submissionKey, nameof(SubmissionKey));
        RequestFingerprint = Rule.Text(requestFingerprint, nameof(RequestFingerprint), 64);
    }
    public Guid CustomerId { get; private set; }
    public Guid SalespersonId { get; private set; }
    public string CustomerName { get; private set; } = "";
    public string SalespersonName { get; private set; } = "";
    public int PaymentTermCode { get; private set; }
    public DateOnly DispatchDate { get; private set; }
    public DispatchWindow DispatchWindow { get; private set; }
    public DateTime SubmittedAtUtc { get; private set; }
    public Guid SubmissionKey { get; private set; }
    public string RequestFingerprint { get; private set; } = "";
    public Guid? AllocationBatchId { get; private set; }
    public OrderStatus Status { get; private set; } = OrderStatus.Submitted;
    public IReadOnlyCollection<OrderItem> Items => _items.AsReadOnly();
    public long RequestedTotalMinor => _items.Sum(x => x.RequestedTotalMinor);

    public void EnsureFutureDispatch(DateTimeOffset now, TimeZoneInfo zone)
    {
        var localNow = TimeZoneInfo.ConvertTime(now, zone).DateTime;
        if (DispatchDate.ToDateTime(new TimeOnly((int)DispatchWindow, 0)) <= localNow)
            throw new DomainRuleException("Dispatch has elapsed. Select a future dispatch date and window.");
    }

    public void Reschedule(DateOnly date, DispatchWindow window, DateTimeOffset now, TimeZoneInfo zone)
    {
        if (Status != OrderStatus.Submitted) throw new DomainRuleException("Finalized orders cannot be rescheduled.");
        Rule.Date(date, nameof(date));
        if (!Enum.IsDefined(window) || date.ToDateTime(new TimeOnly((int)window, 0)) <= TimeZoneInfo.ConvertTime(now, zone).DateTime)
            throw new DomainRuleException("Select a future dispatch date and a 12:00 or 19:00 window.");
        DispatchDate = date; DispatchWindow = window; Changed();
    }

    public void FinalizeAllocation(Guid batchId, IReadOnlyDictionary<Guid, int> quantities,
        DateTimeOffset now, TimeZoneInfo zone)
    {
        Rule.Id(batchId, nameof(batchId));
        EnsureFutureDispatch(now, zone);
        ConfirmQuantities(quantities);
        AllocationBatchId = batchId;
    }

    public void AddItem(Guid productId, string productName, int quantity, long priceMinor)
    {
        if (Status != OrderStatus.Submitted || _items.Count >= 100 || _items.Any(x => x.ProductId == productId))
            throw new DomainRuleException("Order cannot contain duplicate products or more than 100 lines.");
        var item = new OrderItem(Id, productId, productName, quantity, priceMinor);
        if (_items.Sum(x => x.RequestedTotalMinor) + item.RequestedTotalMinor > Money.MaximumMinor)
            throw new DomainRuleException("Order total exceeds the monetary limit.");
        _items.Add(item); Changed();
    }

    public void ConfirmQuantities(IReadOnlyDictionary<Guid, int> quantities)
    {
        if (Status != OrderStatus.Submitted || _items.Count == 0
            || quantities.Count != _items.Count || _items.Any(x => !quantities.ContainsKey(x.Id)))
            throw new DomainRuleException("Confirmation must address every line of a submitted order.");
        foreach (var item in _items)
            if (quantities[item.Id] < 0 || quantities[item.Id] > item.RequestedQuantity)
                throw new DomainRuleException("Fulfilled quantity must be between zero and requested quantity.");
        foreach (var item in _items) item.Fulfill(quantities[item.Id]);
        Status = _items.All(x => x.FulfilledQuantity == x.RequestedQuantity) ? OrderStatus.Fulfilled
            : _items.Any(x => x.FulfilledQuantity > 0) ? OrderStatus.PartiallyFulfilled : OrderStatus.Cut;
        Changed();
    }
}

public sealed class OrderItem : Entity
{
    private OrderItem() { }
    internal OrderItem(Guid orderId, Guid productId, string name, int quantity, long priceMinor)
    {
        OrderId = orderId; ProductId = Rule.Id(productId, nameof(ProductId));
        ProductName = Rule.Text(name, nameof(ProductName));
        RequestedQuantity = Rule.Quantity(quantity); UnitPriceMinor = Money.Validate(priceMinor, true);
        Money.Multiply(priceMinor, quantity);
    }
    public Guid OrderId { get; private set; }
    public Guid ProductId { get; private set; }
    public string ProductName { get; private set; } = "";
    public int RequestedQuantity { get; private set; }
    public int FulfilledQuantity { get; private set; }
    public long UnitPriceMinor { get; private set; }
    public long RequestedTotalMinor => Money.Multiply(UnitPriceMinor, RequestedQuantity);
    internal void Fulfill(int quantity) { FulfilledQuantity = quantity; Changed(); }
}
