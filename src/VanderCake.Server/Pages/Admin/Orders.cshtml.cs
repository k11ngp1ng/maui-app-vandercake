using Microsoft.AspNetCore.Mvc.RazorPages;
using VanderCake.Application.Abstractions;
using VanderCake.Contracts.Api;
namespace VanderCake.Server.Pages.Admin;
public sealed class OrdersModel(IOrderService service) : PageModel
{
    public IReadOnlyList<OrderResponse> Orders { get; private set; } = [];
    public async Task OnGetAsync(string? q, string? status, CancellationToken ct) => Orders = (await service.ListAsync(ct)).Where(o => (string.IsNullOrWhiteSpace(q) || o.CustomerName.Contains(q, StringComparison.OrdinalIgnoreCase)) && (string.IsNullOrWhiteSpace(status) ? o.Status == "Submitted" : status == "All" || o.Status == status)).ToList();
}
