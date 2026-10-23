using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc;
using VanderCake.Application.Abstractions;
using VanderCake.Contracts.Api;
namespace VanderCake.Server.Pages.Admin;
public sealed class ProductsModel(IProductService service, IReferenceDataService refs) : AdminPageModel
{
    [BindProperty(SupportsGet = true)] public Guid? Id { get; set; }
    [BindProperty] public Guid Version { get; set; }
    [BindProperty] public Input Form { get; set; } = new();
    public IReadOnlyList<ProductResponse> Products { get; private set; } = [];
    public IReadOnlyList<ProductGroupResponse> Groups { get; private set; } = [];
    public sealed class Input
    {
        [Required, StringLength(40)] public string Code { get; set; } = "";
        [Required, StringLength(160)] public string Name { get; set; } = "";
        public Guid GroupId { get; set; }
        [Required, StringLength(30)] public string SaleUnit { get; set; } = "unit";
        [Range(typeof(decimal), "0.005", "10000000000", ParseLimitsInInvariantCulture = true, ConvertValueInInvariantCulture = true)] public decimal Price { get; set; }
        public bool IsActive { get; set; } = true;
    }
    private async Task Load(CancellationToken ct) { Products = await service.ListAsync(ct); Groups = await refs.ProductGroupsAsync(ct); }
    public async Task OnGetAsync(string? q, Guid? groupId, CancellationToken ct)
    {
        if (Id is { } id) { var p = await service.GetAsync(id, ct); Version = p.Version;
            Form = new() { Code = p.Code, Name = p.Name, GroupId = p.ProductGroupId, SaleUnit = p.SaleUnit, Price = p.Price, IsActive = p.IsActive }; }
        await Load(ct);
        Products = Products.Where(p => (!groupId.HasValue || p.ProductGroupId == groupId) && (string.IsNullOrWhiteSpace(q) || (p.Name + " " + p.Code).Contains(q, StringComparison.OrdinalIgnoreCase))).ToList();
    }
    public async Task<IActionResult> OnPostAsync(CancellationToken ct)
    {
        if (await AttemptAsync(async () => {
            var request = new ProductRequest(Form.Code, Form.Name, Form.GroupId, Form.SaleUnit, Form.Price, Form.IsActive);
            if (Id is { } id) await service.UpdateAsync(id, new(request, Version), ct); else await service.CreateAsync(request, ct);
        })) return RedirectToPage("Products");
        await Load(ct); return Page();
    }
}
