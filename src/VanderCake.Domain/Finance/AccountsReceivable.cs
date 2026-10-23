using VanderCake.Domain.Common;

namespace VanderCake.Domain.Finance;

public enum FinancialStatus { Open = 0, Paid = 1 }
public enum ReceivableState { Open, Overdue, Paid }

public sealed record FinancialRules
{
    public FinancialRules(int penaltyBasisPoints = 500, int dailyInterestBasisPoints = 200, int dailyDiscountBasisPoints = 100)
    {
        if (new[] { penaltyBasisPoints, dailyInterestBasisPoints, dailyDiscountBasisPoints }.Any(x => x is < 0 or > 10000))
            throw new DomainRuleException("Financial rates must be between zero and 100 percent.");
        PenaltyBasisPoints = penaltyBasisPoints; DailyInterestBasisPoints = dailyInterestBasisPoints;
        DailyDiscountBasisPoints = dailyDiscountBasisPoints;
    }
    public int PenaltyBasisPoints { get; }
    public int DailyInterestBasisPoints { get; }
    public int DailyDiscountBasisPoints { get; }
}

public sealed record SettlementQuote(long PrincipalMinor, long PenaltyMinor, long InterestMinor, long DiscountMinor)
{
    public long AmountMinor => checked(PrincipalMinor + PenaltyMinor + InterestMinor - DiscountMinor);
}

public sealed class AccountsReceivable : Entity
{
    private AccountsReceivable() { }
    public AccountsReceivable(Guid saleId, ScheduledInstallment installment, DateOnly saleDate, FinancialRules rules)
    {
        SaleId = Rule.Id(saleId, nameof(SaleId));
        if (installment.Number <= 0 || installment.BasisPoints is < 1 or > 10000)
            throw new DomainRuleException("Invalid installment.");
        Rule.Date(saleDate, nameof(saleDate));
        DueDate = Rule.Date(installment.DueDate, nameof(DueDate));
        if (DueDate <= saleDate) throw new DomainRuleException("Due date must follow the sale.");
        Number = installment.Number; PrincipalMinor = Money.Validate(installment.PrincipalMinor, true);
        PercentageBasisPoints = installment.BasisPoints;
        PenaltyBasisPoints = rules.PenaltyBasisPoints; DailyInterestBasisPoints = rules.DailyInterestBasisPoints;
        DailyDiscountBasisPoints = rules.DailyDiscountBasisPoints;
    }
    public Guid SaleId { get; private set; }
    public int Number { get; private set; }
    public DateOnly DueDate { get; private set; }
    public long PrincipalMinor { get; private set; }
    public int PercentageBasisPoints { get; private set; }
    public int PenaltyBasisPoints { get; private set; }
    public int DailyInterestBasisPoints { get; private set; }
    public int DailyDiscountBasisPoints { get; private set; }
    public FinancialStatus Status { get; private set; }

    public ReceivableState StateOn(DateOnly date) => Status == FinancialStatus.Paid
        ? ReceivableState.Paid : DueDate < date ? ReceivableState.Overdue : ReceivableState.Open;

    public SettlementQuote Quote(DateOnly paymentDate)
    {
        Rule.Date(paymentDate, nameof(paymentDate));
        if (Status != FinancialStatus.Open) throw new DomainRuleException("Installment is already paid.");
        var days = paymentDate.DayNumber - DueDate.DayNumber;
        long Round(decimal amount) => checked((long)decimal.Round(amount, 0, MidpointRounding.AwayFromZero));
        return days > 0
            ? new(PrincipalMinor, Round(PrincipalMinor * (decimal)PenaltyBasisPoints / 10000m),
                Round(PrincipalMinor * (decimal)DailyInterestBasisPoints * days / 10000m), 0)
            : new(PrincipalMinor, 0, 0, Math.Min(PrincipalMinor,
                Round(PrincipalMinor * (decimal)DailyDiscountBasisPoints * -days / 10000m)));
    }

    public PaymentRecord Settle(long paidMinor, DateOnly paymentDate, Guid administratorId,
        Guid requestKey, DateTime utcTime, bool confirmZero = false)
    {
        var quote = Quote(paymentDate);
        if (paidMinor != quote.AmountMinor || (paidMinor == 0 && !confirmZero))
            throw new DomainRuleException("Payment must match the full settlement quote.");
        var payment = new PaymentRecord(Id, quote, paymentDate, administratorId, requestKey, utcTime);
        Status = FinancialStatus.Paid; Changed();
        return payment;
    }
}

public sealed class PaymentRecord : Entity
{
    private PaymentRecord() { }
    internal PaymentRecord(Guid receivableId, SettlementQuote quote, DateOnly date, Guid administratorId,
        Guid requestKey, DateTime utcTime)
    {
        AccountsReceivableId = receivableId; PaymentDate = date;
        AdministratorId = Rule.Id(administratorId, nameof(AdministratorId));
        RequestKey = Rule.Id(requestKey, nameof(RequestKey));
        if (utcTime.Kind != DateTimeKind.Utc) throw new DomainRuleException("Audit time must be UTC.");
        RecordedAtUtc = utcTime; PrincipalMinor = quote.PrincipalMinor; PenaltyMinor = quote.PenaltyMinor;
        InterestMinor = quote.InterestMinor; DiscountMinor = quote.DiscountMinor; PaidMinor = quote.AmountMinor;
    }
    public Guid AccountsReceivableId { get; private set; }
    public DateOnly PaymentDate { get; private set; }
    public Guid AdministratorId { get; private set; }
    public Guid RequestKey { get; private set; }
    public DateTime RecordedAtUtc { get; private set; }
    public long PrincipalMinor { get; private set; }
    public long PenaltyMinor { get; private set; }
    public long InterestMinor { get; private set; }
    public long DiscountMinor { get; private set; }
    public long PaidMinor { get; private set; }
}
