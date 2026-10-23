using VanderCake.Domain.Common;

namespace VanderCake.Domain.Inventory;

public sealed class InventoryLot : Entity
{
    private InventoryLot() { }
    public InventoryLot(Guid productId, string code, DateOnly receivedDate, DateOnly expirationDate, int quantity)
    {
        ProductId = Rule.Id(productId, nameof(ProductId));
        Code = Rule.Text(code, nameof(Code), 60);
        ReceivedDate = Rule.Date(receivedDate, nameof(ReceivedDate));
        ExpirationDate = Rule.Date(expirationDate, nameof(ExpirationDate));
        if (expirationDate < receivedDate) throw new DomainRuleException("Expiry cannot precede receipt.");
        ReceivedQuantity = AvailableQuantity = Rule.Quantity(quantity);
    }
    public Guid ProductId { get; private set; }
    public string Code { get; private set; } = "";
    public DateOnly ReceivedDate { get; private set; }
    public DateOnly ExpirationDate { get; private set; }
    public int ReceivedQuantity { get; private set; }
    public int AvailableQuantity { get; private set; }

    public bool IsEligible(DateOnly dispatchDate, DateOnly today)
        => AvailableQuantity > 0 && ReceivedDate <= today && ExpirationDate >= dispatchDate && ExpirationDate >= today;

    public void Consume(int quantity, DateOnly dispatchDate, DateOnly today)
    {
        Rule.Quantity(quantity);
        Rule.Date(dispatchDate, nameof(dispatchDate)); Rule.Date(today, nameof(today));
        if (!IsEligible(dispatchDate, today) || quantity > AvailableQuantity)
            throw new DomainRuleException("Lot is expired or has insufficient valid stock.");
        AvailableQuantity -= quantity;
        Changed();
    }
}
