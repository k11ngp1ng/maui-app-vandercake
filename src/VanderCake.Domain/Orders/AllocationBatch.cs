using VanderCake.Domain.Common;

namespace VanderCake.Domain.Orders;

// Gives even a zero-fulfilled confirmation an auditable, uniquely keyed outcome.
public sealed class AllocationBatch : Entity
{
    private AllocationBatch() { }
    public AllocationBatch(Guid requestKey, string fingerprint, Guid administratorId, DateTime utcTime)
    {
        RequestKey = Rule.Id(requestKey, nameof(RequestKey));
        RequestFingerprint = Rule.Text(fingerprint, nameof(RequestFingerprint), 64);
        AdministratorId = Rule.Id(administratorId, nameof(AdministratorId));
        if (utcTime == default || utcTime.Kind != DateTimeKind.Utc)
            throw new DomainRuleException("Confirmation time must be a valid UTC timestamp.");
        ConfirmedAtUtc = utcTime;
    }
    public Guid RequestKey { get; private set; }
    public string RequestFingerprint { get; private set; } = "";
    public Guid AdministratorId { get; private set; }
    public DateTime ConfirmedAtUtc { get; private set; }
}
