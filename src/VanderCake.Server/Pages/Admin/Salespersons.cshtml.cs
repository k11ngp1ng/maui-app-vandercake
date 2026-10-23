using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc;
using VanderCake.Application.Abstractions;
namespace VanderCake.Server.Pages.Admin;
public sealed class SalespersonsModel(IAdministrationService service) : AdminPageModel
{
    [BindProperty(SupportsGet = true)] public Guid? Id { get; set; }
    [BindProperty] public Guid Version { get; set; }
    [BindProperty, Required, StringLength(160)] public string Name { get; set; } = "";
    [BindProperty, EmailAddress, StringLength(254)] public string? Email { get; set; }
    [BindProperty, DataType(DataType.Password), StringLength(200, MinimumLength = 12)] public string? Password { get; set; }
    [BindProperty] public bool IsActive { get; set; } = true;
    public IReadOnlyList<ManagedSalesperson> Salespersons { get; private set; } = [];
    public async Task OnGetAsync(CancellationToken ct)
    {
        Salespersons = await service.SalespersonsAsync(ct);
        if (Id is { } id) { var s = Salespersons.SingleOrDefault(x => x.Id == id) ?? throw new ApplicationFailure(FailureKind.NotFound, "Salesperson was not found."); Name = s.Name; Version = s.Version; IsActive = s.IsActive; Email = s.Email; }
    }
    public async Task<IActionResult> OnPostAsync(CancellationToken ct)
    {
        if (!Id.HasValue && (string.IsNullOrWhiteSpace(Email) || string.IsNullOrEmpty(Password))) ModelState.AddModelError("", "Email and initial password are required for a new account.");
        if (await AttemptAsync(async () => {
            if (Id is { } id) await service.UpdateSalespersonAsync(id, Version, Name, IsActive, ct);
            else await service.CreateSalespersonAsync(Name, Email!, Password!, ct);
        })) return RedirectToPage("Salespersons");
        Password = null; ModelState.Remove(nameof(Password)); Salespersons = await service.SalespersonsAsync(ct); return Page();
    }
}
