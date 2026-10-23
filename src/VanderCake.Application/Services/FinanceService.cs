using VanderCake.Application.Abstractions;
using VanderCake.Contracts.Api;
using VanderCake.Domain.Common;
using VanderCake.Domain.Finance;

namespace VanderCake.Application.Services;

public sealed class FinanceService(IWorkflowStore workflow, IDistributionStore data, AccessScope access,
    ICurrentUser user, BusinessClock clock) : IFinanceService
{
    private async Task<Guid?> CustomerOwnerAsync(Guid customerId, CancellationToken ct)
    {
        var owner = await access.OwnerIdAsync(ct);
        if (await data.CustomerAsync(customerId, owner, ct) is null)
            throw new ApplicationFailure(FailureKind.NotFound, "Customer was not found.");
        return owner;
    }

    public async Task<CustomerBalanceResponse> BalanceAsync(Guid customerId, CancellationToken ct)
    {
        var owner = await CustomerOwnerAsync(customerId, ct);
        var today = clock.Today;
        var rows = (await workflow.ReceivablesAsync(customerId, owner, ct)).Select(d => Map(d, today)).ToList();
        long Sum(IEnumerable<long> values)
        {
            try { return values.Sum(); }
            catch (OverflowException) { throw new DomainRuleException("Balance exceeds the supported monetary range."); }
        }
        return new(customerId, today, Sum(rows.Where(a => a.Status != "Paid").Select(a => a.PrincipalMinor)),
            Sum(rows.Where(a => a.Status == "Overdue").Select(a => a.PrincipalMinor)),
            Sum(rows.Select(a => a.PayableMinor)), rows);
    }

    public async Task<IReadOnlyList<SaleResponse>> PurchasesAsync(Guid customerId, CancellationToken ct)
    {
        var owner = await CustomerOwnerAsync(customerId, ct);
        var today = clock.Today;
        return (await workflow.PurchasesAsync(customerId, owner, ct)).Select(d => Map(d, today)).ToList();
    }

    public async Task<SaleResponse> SaleAsync(Guid id, CancellationToken ct)
        => Map(await workflow.SaleAsync(id, await access.OwnerIdAsync(ct), ct)
            ?? throw new ApplicationFailure(FailureKind.NotFound, "Sale was not found."), clock.Today);

    public async Task<IReadOnlyList<ReceivableResponse>> ReceivablesAsync(Guid? customerId, CancellationToken ct)
    {
        var owner = customerId is { } id ? await CustomerOwnerAsync(id, ct) : await access.OwnerIdAsync(ct);
        var today = clock.Today;
        return (await workflow.ReceivablesAsync(customerId, owner, ct)).Select(d => Map(d, today)).ToList();
    }

    public async Task<ReceivableResponse> ReceivableAsync(Guid id, CancellationToken ct)
        => Map(await workflow.ReceivableAsync(id, await access.OwnerIdAsync(ct), false, ct)
            ?? throw new ApplicationFailure(FailureKind.NotFound, "Installment was not found."), clock.Today);

    public async Task<PaymentResult> PayAsync(Guid receivableId, RegisterPaymentRequest request, CancellationToken ct)
    {
        access.RequireAdministrator();
        Rule.Id(receivableId, nameof(receivableId)); Rule.Id(request.RequestKey, nameof(request.RequestKey));
        Rule.Id(request.Version, nameof(request.Version)); Rule.Date(request.QuoteDate, nameof(request.QuoteDate));
        if (request.AmountMinor < 0) throw new DomainRuleException("Payment cannot be negative.");
        await using var transaction = await workflow.BeginWriteAsync(ct);
        var existing = await workflow.PaymentByRequestAsync(request.RequestKey, ct);
        if (existing is not null)
        {
            if (existing.AccountsReceivableId != receivableId || existing.PaidMinor != request.AmountMinor
                || existing.PaymentDate != request.QuoteDate || (existing.PaidMinor == 0 && !request.ConfirmZero))
                throw new ApplicationFailure(FailureKind.Conflict, "Payment key was already used for different content.");
            return new(Map(existing), false);
        }
        var record = await workflow.ReceivableAsync(receivableId, null, true, ct)
            ?? throw new ApplicationFailure(FailureKind.NotFound, "Installment was not found.");
        var account = record.Account;
        if (account.Status == FinancialStatus.Paid)
            throw new ApplicationFailure(FailureKind.Conflict, "Installment is already paid.");
        if (account.Version != request.Version || request.QuoteDate != clock.Today)
            throw new ApplicationFailure(FailureKind.Conflict, "Settlement quote changed; reload before paying.");
        var now = clock.UtcNow;
        var today = DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(now, BusinessClock.Zone).DateTime);
        if (today != request.QuoteDate)
            throw new ApplicationFailure(FailureKind.Conflict, "Business date changed; reload the settlement quote.");
        var payment = account.Settle(request.AmountMinor, today, user.UserId,
            request.RequestKey, now.UtcDateTime, request.ConfirmZero);
        // Both flushes belong to one transaction, including a failure while inserting payment history.
        await workflow.SaveChangesAsync(ct);
        workflow.Add(payment);
        await workflow.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);
        return new(Map(payment), true);
    }

    public async Task<PaymentResponse> PaymentAsync(Guid id, CancellationToken ct)
        => Map(await workflow.PaymentAsync(id, await access.OwnerIdAsync(ct), ct)
            ?? throw new ApplicationFailure(FailureKind.NotFound, "Payment was not found."));

    private static SaleResponse Map(SaleData d, DateOnly today) => new(d.Sale.Id, d.Sale.OrderId,
        d.Sale.CustomerId, d.Sale.CustomerName, d.Sale.SaleDate, d.Sale.PaymentTermCode, d.Sale.TotalMinor,
        d.Sale.Items.Select(i => new SaleItemResponse(i.ProductId, i.ProductName, i.Quantity, i.UnitPriceMinor, i.TotalMinor)).ToList(),
        d.Installments.Select(a => Map(a, today)).ToList());

    private static ReceivableResponse Map(ReceivableData d, DateOnly today)
    {
        var a = d.Account;
        var quote = a.Status == FinancialStatus.Open ? a.Quote(today) : null;
        return new(a.Id, a.SaleId, d.Sale.CustomerId, d.Sale.CustomerName, a.Number, a.DueDate,
            a.StateOn(today).ToString(), today, a.PrincipalMinor, quote?.AmountMinor ?? 0,
            quote?.PenaltyMinor ?? d.Payment?.PenaltyMinor ?? 0,
            quote?.InterestMinor ?? d.Payment?.InterestMinor ?? 0,
            quote?.DiscountMinor ?? d.Payment?.DiscountMinor ?? 0, a.PenaltyBasisPoints,
            a.DailyInterestBasisPoints, a.DailyDiscountBasisPoints, a.Version, d.Payment is null ? null : Map(d.Payment));
    }

    private static PaymentResponse Map(PaymentRecord p) => new(p.Id, p.AccountsReceivableId, p.RequestKey,
        p.PaymentDate, p.PrincipalMinor, p.PenaltyMinor, p.InterestMinor, p.DiscountMinor, p.PaidMinor,
        p.AdministratorId, DateTime.SpecifyKind(p.RecordedAtUtc, DateTimeKind.Utc));
}
