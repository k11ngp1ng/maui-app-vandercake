namespace VanderCake.Domain.Common;

public abstract class Entity
{
    public Guid Id { get; private set; } = Guid.NewGuid();
    public Guid Version { get; private set; } = Guid.NewGuid();
    protected void Changed() => Version = Guid.NewGuid();
}

public sealed class DomainRuleException(string message) : Exception(message);

public static class Rule
{
    public static string Text(string? value, string name, int maximum = 160)
    {
        if (string.IsNullOrWhiteSpace(value) || value.Trim().Length > maximum)
            throw new DomainRuleException($"{name} is required and must have at most {maximum} characters.");
        return value.Trim();
    }

    public static Guid Id(Guid value, string name)
        => value != Guid.Empty ? value : throw new DomainRuleException($"{name} is required.");

    public static int Quantity(int value)
        => value is > 0 and <= 1_000_000 ? value
            : throw new DomainRuleException("Quantity must be between 1 and 1000000 sale units.");

    public static DateOnly Date(DateOnly value, string name)
        => value != default ? value : throw new DomainRuleException($"{name} is required.");
}

public static class Money
{
    public const long MaximumMinor = 1_000_000_000_000;

    public static long ToMinor(decimal amount)
    {
        if (amount < 0 || amount > MaximumMinor / 100m)
            throw new DomainRuleException("Amount is outside the supported monetary range.");
        return checked((long)decimal.Round(amount * 100m, 0, MidpointRounding.AwayFromZero));
    }

    public static decimal FromMinor(long minor) => Validate(minor) / 100m;

    public static long Validate(long minor, bool positive = false)
    {
        if (minor < (positive ? 1 : 0) || minor > MaximumMinor)
            throw new DomainRuleException("Amount is outside the supported monetary range.");
        return minor;
    }

    public static long Multiply(long unitMinor, int quantity)
    {
        Validate(unitMinor, positive: true);
        Rule.Quantity(quantity);
        var amount = unitMinor * (decimal)quantity;
        if (amount > MaximumMinor) throw new DomainRuleException("Line total exceeds the monetary limit.");
        return (long)amount;
    }
}
