using VanderCake.Domain.Catalog;
using VanderCake.Domain.Common;
using VanderCake.Domain.Customers;
using VanderCake.Domain.Finance;
using VanderCake.Domain.Inventory;
using VanderCake.Domain.Orders;

namespace VanderCake.UnitTests;

public sealed class DomainTests
{
    [Theory]
    [InlineData("pt-BR")]
    [InlineData("en-US")]
    public void ApiPriceValidationIsIndependentOfCulture(string culture)
    {
        var previous = System.Globalization.CultureInfo.CurrentCulture;
        try
        {
            System.Globalization.CultureInfo.CurrentCulture = new(culture);
            var request = new VanderCake.Contracts.Api.ProductRequest("A", "Product", Guid.NewGuid(), "package", 1.005m);
            var results = new List<System.ComponentModel.DataAnnotations.ValidationResult>();
            Assert.True(System.ComponentModel.DataAnnotations.Validator.TryValidateObject(request,
                new System.ComponentModel.DataAnnotations.ValidationContext(request), results, true));
        }
        finally { System.Globalization.CultureInfo.CurrentCulture = previous; }
    }

    private static readonly DateOnly Today = new(2026, 10, 21);
    private static readonly DateTimeOffset Now = new(2026, 10, 21, 15, 0, 0, TimeSpan.Zero);
    private static readonly TimeZoneInfo Zone = TimeZoneInfo.FindSystemTimeZoneById("America/Sao_Paulo");

    [Theory]
    [InlineData("1.005", 101)]
    [InlineData("100.01", 10001)]
    [InlineData("0.004", 0)]
    [InlineData("0.005", 1)]
    public void MoneyRoundsDeterministically(string value, long minor)
    {
        var amount = decimal.Parse(value, System.Globalization.CultureInfo.InvariantCulture);
        Assert.Equal(minor, Money.ToMinor(amount));
        Assert.Equal(minor / 100m, Money.FromMinor(minor));
    }

    [Fact]
    public void MoneyRejectsNegativeOverflowAndZeroPrice()
    {
        Assert.Throws<DomainRuleException>(() => Money.ToMinor(-0.01m));
        Assert.Throws<DomainRuleException>(() => Money.ToMinor(decimal.MaxValue));
        Assert.Throws<DomainRuleException>(() => Money.Multiply(Money.MaximumMinor, 2));
        Assert.Throws<DomainRuleException>(() => new Product("A", "A", Guid.NewGuid(), "unit", 0));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(1000001)]
    public void LotRejectsInvalidQuantity(int quantity)
        => Assert.Throws<DomainRuleException>(() => new InventoryLot(Guid.NewGuid(), "A", Today, Today.AddDays(1), quantity));

    [Fact]
    public void CustomerRequiresOwnerCityVisitOrderAndContact()
    {
        Assert.Throws<DomainRuleException>(() => new Customer("A", Guid.Empty, Guid.NewGuid(), 1, "Address", "Phone"));
        Assert.Throws<DomainRuleException>(() => new Customer("A", Guid.NewGuid(), Guid.Empty, 1, "Address", "Phone"));
        Assert.Throws<DomainRuleException>(() => new Customer("A", Guid.NewGuid(), Guid.NewGuid(), 0, "Address", "Phone"));
        Assert.Throws<DomainRuleException>(() => new Customer("", Guid.NewGuid(), Guid.NewGuid(), 1, "Address", "Phone"));
    }

    [Fact]
    public void LotRejectsInvalidDatesAndNeverGoesNegative()
    {
        Assert.Throws<DomainRuleException>(() => new InventoryLot(Guid.NewGuid(), "A", default, Today, 1));
        Assert.Throws<DomainRuleException>(() => new InventoryLot(Guid.NewGuid(), "A", Today, Today.AddDays(-1), 1));
        var lot = new InventoryLot(Guid.NewGuid(), "A", Today, Today.AddDays(2), 3);
        Assert.Throws<DomainRuleException>(() => lot.Consume(4, Today, Today));
        Assert.Equal(3, lot.AvailableQuantity);
        lot.Consume(3, Today, Today);
        Assert.Equal(0, lot.AvailableQuantity);
        Assert.False(lot.IsEligible(Today, Today));
    }

    [Fact]
    public void ExpiryIsInclusiveButFutureDispatchExcludesEarlierExpiry()
    {
        var lot = new InventoryLot(Guid.NewGuid(), "A", Today, Today, 3);
        Assert.True(lot.IsEligible(Today, Today));
        Assert.False(lot.IsEligible(Today.AddDays(1), Today));
        Assert.Throws<DomainRuleException>(() => lot.Consume(1, Today, Today.AddDays(1)));
    }

    [Theory]
    [InlineData(10, 10001, 5001, 5000, 0)]
    [InlineData(20, 10000, 3333, 3333, 3334)]
    [InlineData(20, 1, 1, 0, 0)]
    public void TermsPreserveExactCentSum(int code, long total, long first, long second, long third)
    {
        var schedule = Term(code).Schedule(total, Today);
        Assert.Equal(total, schedule.Sum(x => x.PrincipalMinor));
        Assert.Equal(new[] { first, second, third }.Where(x => x > 0), schedule.Select(x => x.PrincipalMinor));
        Assert.All(schedule, x => Assert.Equal(Today.AddDays(x.Number * 7), x.DueDate));
    }

    [Fact]
    public void TermsHandleCalendarBoundariesAndInvalidDefinitions()
    {
        var schedule = Term(10).Schedule(100, new DateOnly(2028, 2, 25));
        Assert.Equal(new DateOnly(2028, 3, 3), schedule[0].DueDate);
        Assert.Throws<DomainRuleException>(() => new PaymentTerm(30, "Invalid", (7, 10000)));
        Assert.Throws<DomainRuleException>(() => new PaymentTerm(10, "Invalid", (7, 4999), (14, 5000)));
        Assert.Throws<DomainRuleException>(() => Term(10).Schedule(1, DateOnly.MaxValue));
    }

    [Theory]
    [InlineData(2, 10900, 500, 400, 0)]
    [InlineData(-3, 9700, 0, 0, 300)]
    [InlineData(0, 10000, 0, 0, 0)]
    [InlineData(-200, 0, 0, 0, 10000)]
    public void SettlementUsesSimpleInterestAndCappedDiscount(int days, long amount, long penalty, long interest, long discount)
    {
        var account = Account();
        var quote = account.Quote(Today.AddDays(days));
        Assert.Equal(amount, quote.AmountMinor);
        Assert.Equal(penalty, quote.PenaltyMinor);
        Assert.Equal(interest, quote.InterestMinor);
        Assert.Equal(discount, quote.DiscountMinor);
    }

    [Fact]
    public void SettlementTransitionsOnlyAfterExactValidPayment()
    {
        var account = Account();
        Assert.Throws<DomainRuleException>(() => account.Settle(9999, Today, Guid.NewGuid(), Guid.NewGuid(), Now.UtcDateTime));
        Assert.Equal(FinancialStatus.Open, account.Status);
        var payment = account.Settle(10000, Today, Guid.NewGuid(), Guid.NewGuid(), Now.UtcDateTime);
        Assert.Equal(10000, payment.PaidMinor);
        Assert.Equal(FinancialStatus.Paid, account.Status);
        Assert.Throws<DomainRuleException>(() => account.Quote(Today));
        Assert.Throws<DomainRuleException>(() => new FinancialRules(-1));
        Assert.Throws<DomainRuleException>(() => new FinancialRules(dailyInterestBasisPoints: 10001));
    }

    [Fact]
    public void OrderValidatesDispatchAndLineInvariants()
    {
        Assert.Throws<DomainRuleException>(() => OrderAt(Today, DispatchWindow.Noon));
        Assert.Throws<DomainRuleException>(() => OrderAt(Today.AddDays(1), (DispatchWindow)13));
        var order = NewOrder();
        var productId = Guid.NewGuid();
        order.AddItem(productId, "Product", 5, 1250);
        Assert.Throws<DomainRuleException>(() => order.AddItem(productId, "Product", 1, 1250));
        Assert.Throws<DomainRuleException>(() => order.AddItem(Guid.NewGuid(), "Product", 0, 1250));
        Assert.Throws<DomainRuleException>(() => order.ConfirmQuantities(new Dictionary<Guid, int> { [order.Items.Single().Id] = 6 }));
        Assert.Equal(OrderStatus.Submitted, order.Status);
        Assert.Equal(0, order.Items.Single().FulfilledQuantity);
    }

    [Theory]
    [InlineData(5, OrderStatus.Fulfilled)]
    [InlineData(4, OrderStatus.PartiallyFulfilled)]
    [InlineData(0, OrderStatus.Cut)]
    public void OrderAndSaleUseExplicitStatesAndSnapshots(int fulfilled, OrderStatus expected)
    {
        var order = NewOrder(); order.AddItem(Guid.NewGuid(), "Original", 5, 1250);
        order.ConfirmQuantities(new Dictionary<Guid, int> { [order.Items.Single().Id] = fulfilled });
        Assert.Equal(expected, order.Status);
        Assert.Throws<DomainRuleException>(() => order.ConfirmQuantities(new Dictionary<Guid, int>()));
        if (fulfilled == 0) Assert.Throws<DomainRuleException>(() => new Sale(order, Today));
        else
        {
            var sale = new Sale(order, Today);
            Assert.Equal(fulfilled * 1250, sale.TotalMinor);
            Assert.Equal("Original", sale.Items.Single().ProductName);
        }
    }

    private static PaymentTerm Term(int code) => code == 10
        ? new(10, "10", (7, 5000), (14, 5000))
        : new(20, "20", (7, 3333), (14, 3333), (21, 3334));
    private static AccountsReceivable Account() => new(Guid.NewGuid(),
        new ScheduledInstallment(1, Today, 10000, 5000), Today.AddDays(-7), new FinancialRules());
    private static Order NewOrder() => OrderAt(Today.AddDays(1), DispatchWindow.Evening);
    private static Order OrderAt(DateOnly date, DispatchWindow window) => new(Guid.NewGuid(), Guid.NewGuid(),
        "Customer", "Seller", 10, date, window, Now, Zone, Guid.NewGuid(), "fingerprint");
}
