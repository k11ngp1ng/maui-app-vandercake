using Microsoft.EntityFrameworkCore;
using VanderCake.Application.Abstractions;
using VanderCake.Contracts.Api;

namespace VanderCake.Infrastructure.Persistence;

public sealed class ReferenceDataService(VanderCakeDbContext db) : IReferenceDataService
{
    public async Task<IReadOnlyList<StateResponse>> StatesAsync(CancellationToken ct)
        => await db.States.OrderBy(x => x.Code).Select(x => new StateResponse(x.Id, x.Code, x.Name)).ToListAsync(ct);
    public async Task<IReadOnlyList<CityResponse>> CitiesAsync(CancellationToken ct)
        => await db.Cities.OrderBy(x => x.Name).Select(x => new CityResponse(x.Id, x.StateId, x.Name)).ToListAsync(ct);
    public async Task<IReadOnlyList<ProductGroupResponse>> ProductGroupsAsync(CancellationToken ct)
        => await db.ProductGroups.OrderBy(x => x.Name).Select(x => new ProductGroupResponse(x.Id, x.Name)).ToListAsync(ct);
    public async Task<IReadOnlyList<PaymentTermResponse>> PaymentTermsAsync(CancellationToken ct)
        => (await db.PaymentTerms.Include(x => x.Definitions).OrderBy(x => x.Code).ToListAsync(ct))
            .Select(x => new PaymentTermResponse(x.Code, x.Name, x.Definitions.OrderBy(d => d.Number)
                .Select(d => new InstallmentDefinitionResponse(d.Number, d.Days, d.BasisPoints)).ToList())).ToList();
    public async Task<IReadOnlyList<SalespersonResponse>> SalespersonsAsync(CancellationToken ct)
        => await db.Salespersons.OrderBy(x => x.Name).Select(x => new SalespersonResponse(x.Id, x.Name, x.IsActive)).ToListAsync(ct);
}
