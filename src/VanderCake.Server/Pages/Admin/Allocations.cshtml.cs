using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc;
using VanderCake.Application.Abstractions;
using VanderCake.Application.Services;
using VanderCake.Contracts.Api;
namespace VanderCake.Server.Pages.Admin;
public sealed class AllocationsModel(IFulfillmentService service, BusinessClock clock) : AdminPageModel
{
    [BindProperty(SupportsGet = true)] public DateOnly DispatchDate { get; set; } = clock.Today.AddDays(1);
    [BindProperty, Range(12, 19)] public int DispatchHour { get; set; } = 19;
    [BindProperty] public Guid RequestKey { get; set; }
    [BindProperty, MaxLength(100)] public List<OrderInput> Orders { get; set; } = [];
    [BindProperty] public bool Confirm { get; set; }
    public IReadOnlyList<OrderResponse> Pending { get; private set; } = [];
    public IReadOnlyList<StockResponse> Stock { get; private set; } = [];
    public sealed class OrderInput
    {
        public bool Selected { get; set; }
        public Guid OrderId { get; set; }
        public Guid Version { get; set; }
        [MinLength(1), MaxLength(100)] public List<LineInput> Items { get; set; } = [];
    }
    public sealed class LineInput
    {
        public Guid OrderItemId { get; set; }
        [Range(0, 1_000_000)] public int FulfilledQuantity { get; set; }
    }
    private async Task Load(CancellationToken ct) { Pending = await service.SubmittedAsync(ct); Stock = await service.StockAsync(DispatchDate < clock.Today ? clock.Today : DispatchDate, ct); }
    public async Task OnGetAsync(CancellationToken ct)
    {
        await Load(ct); RequestKey = Guid.NewGuid();
        Orders = Pending.Take(100).Select(o => new OrderInput { OrderId = o.Id, Version = o.Version, Items = o.Items.Select(i => new LineInput { OrderItemId = i.Id }).ToList() }).ToList();
    }
    public async Task<IActionResult> OnPostAsync(CancellationToken ct)
    {
        if (!Confirm) ModelState.AddModelError("", "Confirm that selected orders will be finalized, including shortages.");
        if (!Orders.Any(o => o.Selected)) ModelState.AddModelError("", "Select at least one order.");
        AllocationResult? result = null;
        if (await AttemptAsync(async () => result = await service.ConfirmAsync(new(RequestKey,
            Orders.Where(o => o.Selected).Select(o => new OrderAllocationRequest(o.OrderId, o.Version,
                o.Items.Select(i => new AllocationLineRequest(i.OrderItemId, i.FulfilledQuantity)).ToList(), DispatchDate, DispatchHour)).ToList()), ct)))
            return RedirectToPage("AllocationHistory", new { id = result!.Allocation.Id });
        await Load(ct); return Page();
    }
}
