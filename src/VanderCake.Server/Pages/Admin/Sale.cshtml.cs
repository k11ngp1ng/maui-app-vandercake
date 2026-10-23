using VanderCake.Application.Abstractions;
using VanderCake.Contracts.Api;
namespace VanderCake.Server.Pages.Admin;
public sealed class SaleModel(IFinanceService service) : AdminPageModel
{
    public SaleResponse Sale { get; private set; } = null!;
    public async Task OnGetAsync(Guid id, CancellationToken ct) => Sale = await service.SaleAsync(id, ct);
}
