using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using VanderCake.Application.Abstractions;
using VanderCake.Application.Services;
using VanderCake.Contracts.Api;
using VanderCake.Domain.Common;

namespace VanderCake.Server.Pages.Admin;

public sealed class AllocationModel(IFulfillmentService service, IOrderService orders, BusinessClock clock) : PageModel
{
    [BindProperty(SupportsGet = true)] public Guid Id { get; set; }
    [BindProperty] public Guid Version { get; set; }
    [BindProperty] public Guid RequestKey { get; set; }
    [BindProperty] public DateOnly DispatchDate { get; set; }
    [BindProperty] public int DispatchHour { get; set; }
    [BindProperty, MinLength(1), MaxLength(100)] public List<LineInput> Items { get; set; } = [];
    public OrderResponse? Order { get; private set; }
    public IReadOnlyList<StockResponse> Stock { get; private set; } = [];

    public sealed class LineInput
    {
        public Guid OrderItemId { get; set; }
        [Range(0, 1_000_000)] public int FulfilledQuantity { get; set; }
    }

    public async Task OnGetAsync(CancellationToken ct)
    {
        Order = await orders.GetAsync(Id, ct);
        Version = Order.Version; RequestKey = Guid.NewGuid();
        DispatchDate = Order.DispatchDate; DispatchHour = Order.DispatchHour;
        Items = Order.Items.Select(i => new LineInput { OrderItemId = i.Id }).ToList();
        await LoadStockAsync(ct);
    }

    public async Task<IActionResult> OnPostAsync(CancellationToken ct)
    {
        if (ModelState.IsValid)
        {
            try
            {
                var result = await service.ConfirmAsync(new(RequestKey,
                    [new(Id, Version, Items.Select(i => new AllocationLineRequest(i.OrderItemId, i.FulfilledQuantity)).ToList(),
                        DispatchDate, DispatchHour)]), ct);
                return RedirectToPage("/Admin/AllocationHistory", new { id = result.Allocation.Id });
            }
            catch (Exception error) when (error is DomainRuleException or ApplicationFailure)
            { ModelState.AddModelError("", error.Message); }
        }
        Order = await orders.GetAsync(Id, ct);
        await LoadStockAsync(ct);
        return Page();
    }

    private async Task LoadStockAsync(CancellationToken ct)
    {
        var date = DispatchDate < clock.Today ? clock.Today : DispatchDate;
        Stock = (await service.StockAsync(date, ct)).Where(s => Order!.Items.Any(i => i.ProductId == s.ProductId)).ToList();
    }
}
