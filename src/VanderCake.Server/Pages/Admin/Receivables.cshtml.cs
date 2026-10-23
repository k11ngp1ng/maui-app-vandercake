using Microsoft.AspNetCore.Mvc.RazorPages;
using VanderCake.Application.Abstractions;
using VanderCake.Contracts.Api;
namespace VanderCake.Server.Pages.Admin;
public sealed class ReceivablesModel(IFinanceService service, IAdministrationService admin) : PageModel
{
    public IReadOnlyList<ReceivableResponse> Accounts { get; private set; } = [];
    public long OpenPrincipal { get; private set; }
    public long OverduePrincipal { get; private set; }
    public async Task OnGetAsync(string? q, string? status, Guid? customerId, CancellationToken ct)
    {
        Accounts = (await service.ReceivablesAsync(customerId, ct)).Where(a => (string.IsNullOrWhiteSpace(q) || a.CustomerName.Contains(q, StringComparison.OrdinalIgnoreCase)) && (string.IsNullOrWhiteSpace(status) || a.Status == status)).ToList();
        if (customerId is { } id) { var b = await service.BalanceAsync(id, ct); OpenPrincipal = b.OpenPrincipalMinor; OverduePrincipal = b.OverduePrincipalMinor; }
        else { var d = await admin.DashboardAsync(ct); OpenPrincipal = d.OpenPrincipalMinor; OverduePrincipal = d.OverduePrincipalMinor; }
    }
}
