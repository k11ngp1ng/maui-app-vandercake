using VanderCake.Domain.Common;

namespace VanderCake.Domain.Inventory;

public sealed record LotDraw(InventoryLot Lot, int Quantity);

public static class Fefo
{
    // The requested quantity is already the administrator's approved quantity.
    // This selects lots only; it never chooses how much a customer receives.
    public static IReadOnlyList<LotDraw> Consume(IEnumerable<InventoryLot> lots, int quantity,
        DateOnly dispatchDate, DateOnly today)
    {
        Rule.Date(dispatchDate, nameof(dispatchDate)); Rule.Date(today, nameof(today));
        if (quantity == 0) return [];
        Rule.Quantity(quantity);
        var eligible = lots.Where(x => x.IsEligible(dispatchDate, today))
            .OrderBy(x => x.ExpirationDate).ThenBy(x => x.Id).ToList();
        if (eligible.Sum(x => (long)x.AvailableQuantity) < quantity)
            throw new DomainRuleException("Approved quantity exceeds available valid inventory.");
        var result = new List<LotDraw>();
        var remaining = quantity;
        foreach (var lot in eligible)
        {
            var take = Math.Min(remaining, lot.AvailableQuantity);
            if (take == 0) break;
            lot.Consume(take, dispatchDate, today);
            result.Add(new(lot, take)); remaining -= take;
        }
        return result;
    }
}
