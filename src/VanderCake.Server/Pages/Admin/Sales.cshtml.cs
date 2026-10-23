using VanderCake.Application.Abstractions;
namespace VanderCake.Server.Pages.Admin;
public sealed class SalesModel(IAdministrationService service) : AdminPageModel
{
    public IReadOnlyList<SaleSummary> Sales { get; private set; } = [];
    public async Task OnGetAsync(string? q, CancellationToken ct) => Sales = (await service.SalesAsync(ct)).Where(s => string.IsNullOrWhiteSpace(q) || s.CustomerName.Contains(q, StringComparison.OrdinalIgnoreCase)).ToList();
}
