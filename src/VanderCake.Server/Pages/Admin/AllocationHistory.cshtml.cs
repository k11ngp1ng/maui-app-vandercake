using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using VanderCake.Application.Abstractions;
using VanderCake.Contracts.Api;

namespace VanderCake.Server.Pages.Admin;

public sealed class AllocationHistoryModel(IFulfillmentService service) : PageModel
{
    public AllocationResponse Allocation { get; private set; } = null!;
    public async Task OnGetAsync(Guid id, CancellationToken ct) => Allocation = await service.GetAsync(id, ct);
}
