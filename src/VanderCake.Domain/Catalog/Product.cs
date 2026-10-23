using VanderCake.Domain.Common;

namespace VanderCake.Domain.Catalog;

public sealed class ProductGroup : Entity
{
    private ProductGroup() { }
    public ProductGroup(string name) => Name = Rule.Text(name, nameof(Name), 80);
    public string Name { get; private set; } = "";
    public void Rename(string name) { Name = Rule.Text(name, nameof(Name), 80); Changed(); }
}

public sealed class Product : Entity
{
    private Product() { }
    public Product(string code, string name, Guid groupId, string saleUnit, long priceMinor)
        => Update(code, name, groupId, saleUnit, priceMinor, true);
    public string Code { get; private set; } = "";
    public string Name { get; private set; } = "";
    public Guid ProductGroupId { get; private set; }
    public string SaleUnit { get; private set; } = "";
    public long PriceMinor { get; private set; }
    public bool IsActive { get; private set; } = true;

    public void Update(string code, string name, Guid groupId, string saleUnit, long priceMinor, bool active)
    {
        var validCode = Rule.Text(code, nameof(Code), 40).ToUpperInvariant();
        var validName = Rule.Text(name, nameof(Name));
        var group = Rule.Id(groupId, nameof(ProductGroupId));
        var unit = Rule.Text(saleUnit, nameof(SaleUnit), 30);
        Money.Validate(priceMinor, positive: true);
        Code = validCode; Name = validName; ProductGroupId = group;
        SaleUnit = unit; PriceMinor = priceMinor; IsActive = active;
        Changed();
    }
}
