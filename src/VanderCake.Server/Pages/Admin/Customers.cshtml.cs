using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc;
using VanderCake.Application.Abstractions;
using VanderCake.Contracts.Api;
namespace VanderCake.Server.Pages.Admin;
public sealed class CustomersModel(ICustomerService service, IReferenceDataService refs, IFinanceService finance) : AdminPageModel
{
    [BindProperty(SupportsGet = true)] public Guid? Id { get; set; }
    [BindProperty] public Guid Version { get; set; }
    [BindProperty] public Input Form { get; set; } = new();
    public IReadOnlyList<CustomerResponse> Customers { get; private set; } = [];
    public IReadOnlyList<CityResponse> Cities { get; private set; } = [];
    public IReadOnlyList<StateResponse> States { get; private set; } = [];
    public IReadOnlyList<SalespersonResponse> Salespersons { get; private set; } = [];
    public CustomerBalanceResponse? Balance { get; private set; }
    public sealed class Input
    {
        [Required, StringLength(160)] public string Name { get; set; } = "";
        public Guid CityId { get; set; }
        public Guid SalespersonId { get; set; }
        [Range(1, int.MaxValue)] public int VisitOrder { get; set; } = 1;
        [Required, StringLength(240)] public string Address { get; set; } = "";
        [Required, StringLength(30)] public string Phone { get; set; } = "";
        public bool IsActive { get; set; } = true;
    }
    private async Task Load(CancellationToken ct)
    {
        Customers = await service.ListAsync(ct); Cities = await refs.CitiesAsync(ct); States = await refs.StatesAsync(ct); Salespersons = await refs.SalespersonsAsync(ct);
        if (Id is { } id) Balance = await finance.BalanceAsync(id, ct);
    }
    public async Task OnGetAsync(string? q, CancellationToken ct)
    {
        if (Id is { } id) { var c = await service.GetAsync(id, ct); Version = c.Version;
            Form = new() { Name = c.Name, CityId = c.CityId, SalespersonId = c.SalespersonId, VisitOrder = c.VisitOrder, Address = c.Address, Phone = c.Phone, IsActive = c.IsActive }; }
        await Load(ct);
        Customers = Customers.Where(c => string.IsNullOrWhiteSpace(q) || c.Name.Contains(q, StringComparison.OrdinalIgnoreCase)).ToList();
    }
    public async Task<IActionResult> OnPostAsync(CancellationToken ct)
    {
        if (await AttemptAsync(async () => {
            var request = new CustomerRequest(Form.Name, Form.CityId, Form.VisitOrder, Form.Address, Form.Phone, Form.SalespersonId, Form.IsActive);
            if (Id is { } id) await service.UpdateAsync(id, new(request, Version), ct); else await service.CreateAsync(request, ct);
        })) return RedirectToPage("Customers");
        await Load(ct); return Page();
    }
}
