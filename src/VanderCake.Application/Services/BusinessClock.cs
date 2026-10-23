namespace VanderCake.Application.Services;

public sealed class BusinessClock(TimeProvider time)
{
    public static TimeZoneInfo Zone { get; } = TimeZoneInfo.FindSystemTimeZoneById("America/Sao_Paulo");
    public DateTimeOffset UtcNow => time.GetUtcNow();
    public DateOnly Today => DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(UtcNow, Zone).DateTime);
}
