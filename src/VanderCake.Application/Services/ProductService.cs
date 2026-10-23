using VanderCake.Application.Abstractions;
using VanderCake.Contracts.Api;
using VanderCake.Domain.Catalog;
using VanderCake.Domain.Common;

namespace VanderCake.Application.Services;

public sealed class ProductService(IDistributionStore store, AccessScope access) : IProductService
{
    public async Task<IReadOnlyList<ProductResponse>> ListAsync(CancellationToken ct)
    {
        await access.OwnerIdAsync(ct);
        return (await store.ProductsAsync(access.IsAdministrator, ct)).Select(Map).ToList();
    }
    public async Task<ProductResponse> GetAsync(Guid id, CancellationToken ct)
    {
        await access.OwnerIdAsync(ct);
        return Map(await store.ProductAsync(id, access.IsAdministrator, ct)
            ?? throw new ApplicationFailure(FailureKind.NotFound, "Product was not found."));
    }
    public async Task<ProductResponse> CreateAsync(ProductRequest request, CancellationToken ct)
    {
        access.RequireAdministrator();
        if (!await store.ProductGroupExistsAsync(request.ProductGroupId, ct))
            throw new DomainRuleException("Select an existing product group.");
        var product = new Product(request.Code, request.Name, request.ProductGroupId, request.SaleUnit, Money.ToMinor(request.Price));
        product.Update(request.Code, request.Name, request.ProductGroupId, request.SaleUnit, Money.ToMinor(request.Price), request.IsActive);
        store.Add(product); await store.SaveChangesAsync(ct);
        return Map(product);
    }
    public async Task<ProductResponse> UpdateAsync(Guid id, UpdateProductRequest request, CancellationToken ct)
    {
        access.RequireAdministrator();
        var product = await store.ProductAsync(id, true, ct)
            ?? throw new ApplicationFailure(FailureKind.NotFound, "Product was not found.");
        if (product.Version != request.Version)
            throw new ApplicationFailure(FailureKind.Conflict, "Product changed; reload before saving.");
        var r = request.Product;
        if (!await store.ProductGroupExistsAsync(r.ProductGroupId, ct))
            throw new DomainRuleException("Select an existing product group.");
        product.Update(r.Code, r.Name, r.ProductGroupId, r.SaleUnit, Money.ToMinor(r.Price), r.IsActive);
        await store.SaveChangesAsync(ct);
        return Map(product);
    }
    private static ProductResponse Map(Product p)
        => new(p.Id, p.Code, p.Name, p.ProductGroupId, p.SaleUnit, p.PriceMinor,
            Money.FromMinor(p.PriceMinor), p.IsActive, p.Version);
}
