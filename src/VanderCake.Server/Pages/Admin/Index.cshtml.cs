using VanderCake.Application.Abstractions;
namespace VanderCake.Server.Pages.Admin;
public sealed class IndexModel(IAdministrationService service) : AdminPageModel
{
    public DashboardData Metrics { get; private set; } = null!;
    public async Task OnGetAsync(CancellationToken ct) => Metrics = await service.DashboardAsync(ct);
}
