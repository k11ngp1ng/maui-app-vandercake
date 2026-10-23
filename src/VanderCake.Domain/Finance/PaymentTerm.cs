using VanderCake.Domain.Common;

namespace VanderCake.Domain.Finance;

public sealed class PaymentTerm
{
    private readonly List<InstallmentDefinition> _definitions = [];
    private PaymentTerm() { }
    public PaymentTerm(int code, string name, params (int Days, int BasisPoints)[] installments)
    {
        if (code is not (10 or 20)) throw new DomainRuleException("Payment term must be 10 or 20.");
        if (installments.Length is < 1 or > 12
            || installments.Any(x => x.Days is < 1 or > 365 || x.BasisPoints is < 1 or > 10000)
            || installments.Sum(x => x.BasisPoints) != 10000
            || installments.Select(x => x.Days).Distinct().Count() != installments.Length)
            throw new DomainRuleException("Installments must have distinct positive due offsets and total 100 percent.");
        Code = code; Name = Rule.Text(name, nameof(Name), 80);
        foreach (var (days, basisPoints) in installments.OrderBy(x => x.Days))
            _definitions.Add(new InstallmentDefinition(code, _definitions.Count + 1, days, basisPoints));
    }
    public int Code { get; private set; }
    public string Name { get; private set; } = "";
    public IReadOnlyCollection<InstallmentDefinition> Definitions => _definitions.AsReadOnly();

    public IReadOnlyList<ScheduledInstallment> Schedule(long totalMinor, DateOnly saleDate)
    {
        Money.Validate(totalMinor, positive: true); Rule.Date(saleDate, nameof(saleDate));
        var result = new List<ScheduledInstallment>();
        long remaining = totalMinor;
        var ordered = _definitions.OrderBy(x => x.Number).ToArray();
        foreach (var definition in ordered)
        {
            var amount = definition.Number == ordered.Length ? remaining
                : Math.Min(remaining, (long)decimal.Round(totalMinor * (decimal)definition.BasisPoints / 10000m,
                    0, MidpointRounding.AwayFromZero));
            if (saleDate.DayNumber > DateOnly.MaxValue.DayNumber - definition.Days)
                throw new DomainRuleException("Due date exceeds supported date range.");
            if (amount > 0)
                result.Add(new(definition.Number, saleDate.AddDays(definition.Days), amount, definition.BasisPoints));
            remaining -= amount;
        }
        return result;
    }
}

public sealed class InstallmentDefinition
{
    private InstallmentDefinition() { }
    internal InstallmentDefinition(int termCode, int number, int days, int basisPoints)
    { PaymentTermCode = termCode; Number = number; Days = days; BasisPoints = basisPoints; }
    public int PaymentTermCode { get; private set; }
    public int Number { get; private set; }
    public int Days { get; private set; }
    public int BasisPoints { get; private set; }
}

public sealed record ScheduledInstallment(int Number, DateOnly DueDate, long PrincipalMinor, int BasisPoints);
