using VanderCake.Domain.Common;
using VanderCake.Domain.Orders;

namespace VanderCake.Domain.Finance;

public sealed class Sale : Entity
{
    private readonly List<SaleItem> _items = [];
    private Sale() { }
    public Sale(Order order, DateOnly saleDate)
    {
        if (order.Status is not (OrderStatus.Fulfilled or OrderStatus.PartiallyFulfilled))
            throw new DomainRuleException("Only positively fulfilled orders generate sales.");
        OrderId = order.Id; CustomerId = order.CustomerId; SalespersonId = order.SalespersonId;
        CustomerName = order.CustomerName; SalespersonName = order.SalespersonName;
        SaleDate = Rule.Date(saleDate, nameof(SaleDate)); PaymentTermCode = order.PaymentTermCode;
        foreach (var item in order.Items.Where(x => x.FulfilledQuantity > 0))
            _items.Add(new SaleItem(Id, item));
        TotalMinor = _items.Sum(x => x.TotalMinor);
        Money.Validate(TotalMinor, true);
    }
    public Guid OrderId { get; private set; }
    public Guid CustomerId { get; private set; }
    public Guid SalespersonId { get; private set; }
    public string CustomerName { get; private set; } = "";
    public string SalespersonName { get; private set; } = "";
    public int PaymentTermCode { get; private set; }
    public DateOnly SaleDate { get; private set; }
    public long TotalMinor { get; private set; }
    public IReadOnlyCollection<SaleItem> Items => _items.AsReadOnly();
}

public sealed class SaleItem : Entity
{
    private SaleItem() { }
    internal SaleItem(Guid saleId, OrderItem item)
    {
        SaleId = saleId; OrderItemId = item.Id; ProductId = item.ProductId; ProductName = item.ProductName;
        Quantity = item.FulfilledQuantity; UnitPriceMinor = item.UnitPriceMinor;
        TotalMinor = Money.Multiply(UnitPriceMinor, Quantity);
    }
    public Guid SaleId { get; private set; }
    public Guid OrderItemId { get; private set; }
    public Guid ProductId { get; private set; }
    public string ProductName { get; private set; } = "";
    public int Quantity { get; private set; }
    public long UnitPriceMinor { get; private set; }
    public long TotalMinor { get; private set; }
}

public sealed class LotConsumption : Entity
{
    private LotConsumption() { }
    public LotConsumption(Guid orderItemId, Guid lotId, int quantity, Guid administratorId, DateTime utcTime)
    {
        OrderItemId = Rule.Id(orderItemId, nameof(OrderItemId)); InventoryLotId = Rule.Id(lotId, nameof(InventoryLotId));
        Quantity = Rule.Quantity(quantity); AdministratorId = Rule.Id(administratorId, nameof(AdministratorId));
        if (utcTime.Kind != DateTimeKind.Utc) throw new DomainRuleException("Audit time must be UTC.");
        ConsumedAtUtc = utcTime;
    }
    public Guid OrderItemId { get; private set; }
    public Guid InventoryLotId { get; private set; }
    public int Quantity { get; private set; }
    public Guid AdministratorId { get; private set; }
    public DateTime ConsumedAtUtc { get; private set; }
}
