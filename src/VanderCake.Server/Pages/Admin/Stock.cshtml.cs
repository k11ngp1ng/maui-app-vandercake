using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc;
using VanderCake.Application.Abstractions;
using VanderCake.Application.Services;
using VanderCake.Contracts.Api;
namespace VanderCake.Server.Pages.Admin;
public sealed class StockModel(IFulfillmentService service, IProductService products, IAdministrationService admin, BusinessClock clock) : AdminPageModel
{
    [BindProperty(SupportsGet = true)] public DateOnly? DispatchDate { get; set; }
    [BindProperty] public Guid ProductId { get; set; }
    [BindProperty, Required, StringLength(60)] public string Code { get; set; } = "";
    [BindProperty] public DateOnly ReceivedDate { get; set; } = clock.Today;
    [BindProperty] public DateOnly ExpirationDate { get; set; } = clock.Today.AddDays(30);
    [BindProperty, Range(1, 1_000_000)] public int Quantity { get; set; } = 1;
    public IReadOnlyList<StockResponse> Stock { get; private set; } = [];
    public IReadOnlyList<ProductResponse> Products { get; private set; } = [];
    private async Task Load(CancellationToken ct) { Stock = await service.StockAsync(DispatchDate, ct); Products = await products.ListAsync(ct); }
    public Task OnGetAsync(CancellationToken ct) => Load(ct);
    public async Task<IActionResult> OnPostAsync(CancellationToken ct)
    {
        if (await AttemptAsync(() => admin.ReceiveLotAsync(ProductId, Code, ReceivedDate, ExpirationDate, Quantity, ct))) return RedirectToPage("Stock");
        await Load(ct); return Page();
    }
}
