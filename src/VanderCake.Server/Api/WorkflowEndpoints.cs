using VanderCake.Application.Abstractions;
using VanderCake.Contracts.Api;
using VanderCake.Domain.Finance;
using VanderCake.Server.Security;

namespace VanderCake.Server.Api;

public static class WorkflowEndpoints
{
    public static void MapWorkflowEndpoints(this RouteGroupBuilder api)
    {
        var read = api.MapGroup("").RequireAuthorization(Policies.ApiUser);
        read.MapGet("/customers/{id:guid}/balance", (Guid id, IFinanceService s, CancellationToken ct) => s.BalanceAsync(id, ct));
        read.MapGet("/customers/{id:guid}/purchases", (Guid id, IFinanceService s, CancellationToken ct) => s.PurchasesAsync(id, ct));
        read.MapGet("/sales/{id:guid}", (Guid id, IFinanceService s, CancellationToken ct) => s.SaleAsync(id, ct));
        read.MapGet("/receivables", (Guid? customerId, IFinanceService s, CancellationToken ct) => s.ReceivablesAsync(customerId, ct));
        read.MapGet("/receivables/{id:guid}", (Guid id, IFinanceService s, CancellationToken ct) => s.ReceivableAsync(id, ct));
        read.MapGet("/payments/{id:guid}", (Guid id, IFinanceService s, CancellationToken ct) => s.PaymentAsync(id, ct));

        var admin = api.MapGroup("/admin").RequireAuthorization(Policies.ApiAdmin);
        admin.MapGet("/orders/submitted", (IFulfillmentService s, CancellationToken ct) => s.SubmittedAsync(ct));
        admin.MapGet("/stock", (DateOnly? dispatchDate, IFulfillmentService s, CancellationToken ct) => s.StockAsync(dispatchDate, ct));
        admin.MapPost("/allocations/confirm", async (ConfirmAllocationRequest request, IFulfillmentService s, CancellationToken ct) =>
        {
            var result = await s.ConfirmAsync(request, ct);
            return result.Created ? Results.Created($"/api/admin/allocations/{result.Allocation.Id}", result.Allocation)
                : Results.Ok(result.Allocation);
        });
        admin.MapGet("/allocations/{id:guid}", (Guid id, IFulfillmentService s, CancellationToken ct) => s.GetAsync(id, ct));
        admin.MapPost("/receivables/{id:guid}/payments", async (Guid id, RegisterPaymentRequest request, IFinanceService s, CancellationToken ct) =>
        {
            var result = await s.PayAsync(id, request, ct);
            return result.Created ? Results.Created($"/api/payments/{result.Payment.Id}", result.Payment)
                : Results.Ok(result.Payment);
        });
        admin.MapGet("/finance/rules", (FinancialRules rules) => new FinanceRulesResponse(rules.PenaltyBasisPoints,
            rules.DailyInterestBasisPoints, rules.DailyDiscountBasisPoints));
    }
}
