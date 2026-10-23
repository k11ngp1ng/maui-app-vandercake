using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using VanderCake.Application.Abstractions;
using VanderCake.Contracts.Api;
using VanderCake.Domain.Common;

namespace VanderCake.Server.Pages.Admin;

public sealed class PaymentModel(IFinanceService service) : PageModel
{
    [BindProperty(SupportsGet = true)] public Guid Id { get; set; }
    [BindProperty] public Guid Version { get; set; }
    [BindProperty] public Guid RequestKey { get; set; }
    [BindProperty] public DateOnly QuoteDate { get; set; }
    [BindProperty] public long AmountMinor { get; set; }
    [BindProperty] public bool ConfirmZero { get; set; }
    public ReceivableResponse Account { get; private set; } = null!;

    public async Task OnGetAsync(CancellationToken ct)
    {
        Account = await service.ReceivableAsync(Id, ct);
        Version = Account.Version; RequestKey = Guid.NewGuid();
        QuoteDate = Account.QuoteDate; AmountMinor = Account.PayableMinor;
    }

    public async Task<IActionResult> OnPostAsync(CancellationToken ct)
    {
        if (ModelState.IsValid)
        {
            try
            {
                await service.PayAsync(Id, new(RequestKey, Version, QuoteDate, AmountMinor, ConfirmZero), ct);
                return RedirectToPage("/Admin/Payment", new { id = Id });
            }
            catch (Exception error) when (error is DomainRuleException or ApplicationFailure)
            { ModelState.AddModelError("", error.Message); }
        }
        Account = await service.ReceivableAsync(Id, ct);
        return Page();
    }
}
