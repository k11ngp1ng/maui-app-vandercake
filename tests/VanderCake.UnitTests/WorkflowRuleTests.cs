using VanderCake.Domain.Common;
using VanderCake.Domain.Finance;
using VanderCake.Domain.Inventory;

namespace VanderCake.UnitTests;

public sealed class WorkflowRuleTests
{
    private static readonly DateOnly SaleDate = new(2026, 10, 19);

    [Fact]
    public void FIN02_Required235ReaisExampleHasExactAmountsAndCalendarDueDates()
    {
        var term = new PaymentTerm(10, "7/14", (7, 5000), (14, 5000));
        var installments = term.Schedule(Money.ToMinor(235m), SaleDate);
        Assert.Equal(new long[] { 11750, 11750 }, installments.Select(i => i.PrincipalMinor));
        Assert.Equal(new[] { new DateOnly(2026, 10, 26), new DateOnly(2026, 11, 2) }, installments.Select(i => i.DueDate));
    }

    [Fact]
    public void STK02_FefoConsumesEarliestValidLotAndUsesIdForExpiryTies()
    {
        var product = Guid.NewGuid();
        var expired = new InventoryLot(product, "EXPIRED", SaleDate.AddDays(-5), SaleDate.AddDays(-1), 100);
        var later = new InventoryLot(product, "LATER", SaleDate, SaleDate.AddDays(20), 9);
        var a = new InventoryLot(product, "A", SaleDate, SaleDate.AddDays(5), 3);
        var b = new InventoryLot(product, "B", SaleDate, SaleDate.AddDays(5), 3);
        var first = new[] { a, b }.OrderBy(l => l.Id).First();
        var draws = Fefo.Consume([later, b, expired, a], 7, SaleDate.AddDays(1), SaleDate);
        Assert.Equal(first.Id, draws[0].Lot.Id);
        Assert.Equal(new[] { 3, 3, 1 }, draws.Select(d => d.Quantity));
        Assert.Equal(100, expired.AvailableQuantity);
        Assert.Equal(8, later.AvailableQuantity);
    }

    [Fact]
    public void STK02_StockShortageDoesNotPartiallyMutateLots()
    {
        var lot = new InventoryLot(Guid.NewGuid(), "A", SaleDate, SaleDate.AddDays(10), 2);
        Assert.Throws<DomainRuleException>(() => Fefo.Consume([lot], 3, SaleDate.AddDays(1), SaleDate));
        Assert.Equal(2, lot.AvailableQuantity);
        Assert.Empty(Fefo.Consume([lot], 0, SaleDate, SaleDate));
    }

    [Fact]
    public void STK02_UnreceivedStockAndPreDispatchExpiryAreIneligible()
    {
        var future = new InventoryLot(Guid.NewGuid(), "Future", SaleDate.AddDays(1), SaleDate.AddDays(10), 5);
        var early = new InventoryLot(Guid.NewGuid(), "Early", SaleDate, SaleDate.AddDays(2), 5);
        Assert.False(future.IsEligible(SaleDate.AddDays(2), SaleDate));
        Assert.False(early.IsEligible(SaleDate.AddDays(3), SaleDate));
        Assert.True(early.IsEligible(SaleDate.AddDays(2), SaleDate));
    }

    [Theory]
    [InlineData(0, 10000, 0, 0, 0)]
    [InlineData(1, 10700, 500, 200, 0)]
    [InlineData(2, 10900, 500, 400, 0)]
    [InlineData(-1, 9900, 0, 0, 100)]
    [InlineData(-100, 0, 0, 0, 10000)]
    [InlineData(-101, 0, 0, 0, 10000)]
    public void FIN03_DayBoundariesApplyOnePenaltySimpleInterestOrCappedDiscount(
        int days, long payable, long penalty, long interest, long discount)
    {
        var due = SaleDate.AddDays(7);
        var account = Account(10000);
        var quote = account.Quote(due.AddDays(days));
        Assert.Equal(payable, quote.AmountMinor);
        Assert.Equal(penalty, quote.PenaltyMinor);
        Assert.Equal(interest, quote.InterestMinor);
        Assert.Equal(discount, quote.DiscountMinor);
        Assert.True(quote.AmountMinor >= 0);
    }

    [Fact]
    public void FIN03_OneCentRoundingUsesAwayFromZero()
    {
        var account = Account(1);
        Assert.Equal(1, account.Quote(SaleDate.AddDays(7 + 25)).InterestMinor);
        Assert.Equal(1, account.Quote(SaleDate.AddDays(7 - 50)).DiscountMinor);
        var zeroRates = Account(10001, new FinancialRules(0, 0, 0));
        Assert.Equal(10001, zeroRates.Quote(SaleDate.AddDays(100)).AmountMinor);
    }

    [Fact]
    public void FIN04_OverdueIsDerivedAndZeroSettlementNeedsExplicitConfirmation()
    {
        var account = Account(10000);
        Assert.Equal(ReceivableState.Open, account.StateOn(SaleDate.AddDays(7)));
        Assert.Equal(ReceivableState.Overdue, account.StateOn(SaleDate.AddDays(8)));
        var zeroDate = SaleDate.AddDays(-100);
        Assert.Throws<DomainRuleException>(() => account.Settle(0, zeroDate, Guid.NewGuid(), Guid.NewGuid(), DateTime.UtcNow));
        Assert.Equal(FinancialStatus.Open, account.Status);
        account.Settle(0, zeroDate, Guid.NewGuid(), Guid.NewGuid(), DateTime.UtcNow, confirmZero: true);
        Assert.Equal(ReceivableState.Paid, account.StateOn(SaleDate.AddDays(8)));
    }

    private static AccountsReceivable Account(long minor, FinancialRules? rules = null)
        => new(Guid.NewGuid(), new ScheduledInstallment(1, SaleDate.AddDays(7), minor, 5000), SaleDate, rules ?? new());
}
