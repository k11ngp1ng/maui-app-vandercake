using VanderCake.Application.Abstractions;
using VanderCake.Contracts.Api;
using VanderCake.Domain.Common;
using VanderCake.Domain.Customers;

namespace VanderCake.Application.Services;

public sealed class CustomerService(IDistributionStore store, AccessScope access) : ICustomerService
{
    public async Task<IReadOnlyList<CustomerResponse>> ListAsync(CancellationToken ct)
        => (await store.CustomersAsync(await access.OwnerIdAsync(ct), ct)).Select(Map).ToList();

    public async Task<CustomerResponse> GetAsync(Guid id, CancellationToken ct)
        => Map(await OwnedAsync(id, ct));

    private async Task<Customer> OwnedAsync(Guid id, CancellationToken ct)
        => await store.CustomerAsync(id, await access.OwnerIdAsync(ct), ct)
            ?? throw new ApplicationFailure(FailureKind.NotFound, "Customer was not found.");

    private async Task<Guid> ValidateOwnerAsync(CustomerRequest request, CancellationToken ct)
    {
        var ownerId = await access.OwnerIdAsync(ct);
        if (ownerId is not null && request.SalespersonId is not null && request.SalespersonId != ownerId)
            throw new ApplicationFailure(FailureKind.Forbidden, "Customer cannot be assigned to another salesperson.");
        var selected = ownerId ?? request.SalespersonId
            ?? throw new DomainRuleException("Administrator must select a salesperson.");
        var salesperson = await store.FindSalespersonAsync(selected, ct);
        if (salesperson is null || !salesperson.IsActive)
            throw new DomainRuleException("Select an active salesperson.");
        if (!await store.CityExistsAsync(request.CityId, ct))
            throw new DomainRuleException("Select an existing city.");
        return selected;
    }

    public async Task<CustomerResponse> CreateAsync(CustomerRequest request, CancellationToken ct)
    {
        var owner = await ValidateOwnerAsync(request, ct);
        var customer = new Customer(request.Name, owner, request.CityId, request.VisitOrder, request.Address, request.Phone);
        if (!request.IsActive)
            customer.Update(request.Name, owner, request.CityId, request.VisitOrder, request.Address, request.Phone, false);
        store.Add(customer); await store.SaveChangesAsync(ct);
        return Map(customer);
    }

    public async Task<CustomerResponse> UpdateAsync(Guid id, UpdateCustomerRequest request, CancellationToken ct)
    {
        var customer = await OwnedAsync(id, ct);
        if (request.Version != customer.Version)
            throw new ApplicationFailure(FailureKind.Conflict, "Customer changed; reload before saving.");
        var owner = await ValidateOwnerAsync(request.Customer, ct);
        customer.Update(request.Customer.Name, owner, request.Customer.CityId, request.Customer.VisitOrder,
            request.Customer.Address, request.Customer.Phone, request.Customer.IsActive);
        await store.SaveChangesAsync(ct);
        return Map(customer);
    }

    private static CustomerResponse Map(Customer c)
        => new(c.Id, c.Name, c.SalespersonId, c.CityId, c.VisitOrder, c.Address, c.Phone, c.IsActive, c.Version);
}
