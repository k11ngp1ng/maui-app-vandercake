using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc;
using VanderCake.Application.Abstractions;
namespace VanderCake.Server.Pages.Admin;
public sealed class GroupsModel(IAdministrationService service) : AdminPageModel
{
    [BindProperty(SupportsGet = true)] public Guid? Id { get; set; }
    [BindProperty] public Guid Version { get; set; }
    [BindProperty, Required, StringLength(80)] public string Name { get; set; } = "";
    public IReadOnlyList<ManagedGroup> Groups { get; private set; } = [];
    public async Task OnGetAsync(CancellationToken ct)
    {
        Groups = await service.GroupsAsync(ct);
        if (Id is { } id) { var g = Groups.SingleOrDefault(g => g.Id == id) ?? throw new ApplicationFailure(FailureKind.NotFound, "Group was not found."); Name = g.Name; Version = g.Version; }
    }
    public async Task<IActionResult> OnPostAsync(CancellationToken ct)
    {
        if (await AttemptAsync(() => service.SaveGroupAsync(Id, Version, Name, ct))) return RedirectToPage("Groups");
        Groups = await service.GroupsAsync(ct); return Page();
    }
}
