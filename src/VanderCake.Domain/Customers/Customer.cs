using VanderCake.Domain.Common;

namespace VanderCake.Domain.Customers;

public static class RoleNames
{
    public const string Administrator = "Administrator";
    public const string Salesperson = "Salesperson";
}

public sealed class Salesperson : Entity
{
    private Salesperson() { }
    public Salesperson(Guid userId, string name)
    {
        UserId = Rule.Id(userId, nameof(UserId));
        Name = Rule.Text(name, nameof(Name));
    }
    public Guid UserId { get; private set; }
    public string Name { get; private set; } = "";
    public bool IsActive { get; private set; } = true;
    public void Deactivate() { IsActive = false; Changed(); }
    public void Update(string name, bool active)
    {
        Name = Rule.Text(name, nameof(Name)); IsActive = active; Changed();
    }
}

public sealed class State : Entity
{
    private State() { }
    public State(string code, string name)
    {
        Code = Rule.Text(code, nameof(Code), 2).ToUpperInvariant();
        if (Code.Length != 2 || !Code.All(char.IsAsciiLetter))
            throw new DomainRuleException("State code must contain two letters.");
        Name = Rule.Text(name, nameof(Name));
    }
    public string Code { get; private set; } = "";
    public string Name { get; private set; } = "";
}

public sealed class City : Entity
{
    private City() { }
    public City(Guid stateId, string name)
    {
        StateId = Rule.Id(stateId, nameof(StateId));
        Name = Rule.Text(name, nameof(Name));
    }
    public Guid StateId { get; private set; }
    public string Name { get; private set; } = "";
}

public sealed class Customer : Entity
{
    private Customer() { }
    public Customer(string name, Guid salespersonId, Guid cityId, int visitOrder,
        string address, string phone)
        => Update(name, salespersonId, cityId, visitOrder, address, phone, true);

    public string Name { get; private set; } = "";
    public Guid SalespersonId { get; private set; }
    public Guid CityId { get; private set; }
    public int VisitOrder { get; private set; }
    public string Address { get; private set; } = "";
    public string Phone { get; private set; } = "";
    public bool IsActive { get; private set; } = true;

    public void Update(string name, Guid salespersonId, Guid cityId, int visitOrder,
        string address, string phone, bool isActive)
    {
        if (visitOrder <= 0) throw new DomainRuleException("Visit order must be positive.");
        var validName = Rule.Text(name, nameof(Name));
        var owner = Rule.Id(salespersonId, nameof(SalespersonId));
        var city = Rule.Id(cityId, nameof(CityId));
        var validAddress = Rule.Text(address, nameof(Address), 240);
        var validPhone = Rule.Text(phone, nameof(Phone), 30);
        Name = validName; SalespersonId = owner; CityId = city; VisitOrder = visitOrder;
        Address = validAddress; Phone = validPhone; IsActive = isActive;
        Changed();
    }
}
