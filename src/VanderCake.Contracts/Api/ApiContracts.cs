using System.ComponentModel.DataAnnotations;

namespace VanderCake.Contracts.Api;

public sealed record LoginRequest(
    [property: Required, EmailAddress, StringLength(254)] string Email,
    [property: Required, StringLength(200)] string Password);

public sealed record UserResponse(Guid Id, string Email, IReadOnlyList<string> Roles);
// Identity's protected bearer response. Refresh tokens are deliberately not used by this MVP.
public sealed record BearerLoginResponse(string TokenType, string AccessToken, long ExpiresIn);
public sealed record CustomerRequest(
    [property: Required, StringLength(160)] string Name,
    Guid CityId,
    [property: Range(1, int.MaxValue)] int VisitOrder,
    [property: Required, StringLength(240)] string Address,
    [property: Required, StringLength(30)] string Phone,
    Guid? SalespersonId = null,
    bool IsActive = true);
public sealed record UpdateCustomerRequest([property: Required] CustomerRequest Customer, Guid Version);
public sealed record CustomerResponse(Guid Id, string Name, Guid SalespersonId, Guid CityId,
    int VisitOrder, string Address, string Phone, bool IsActive, Guid Version);

public sealed record ProductRequest(
    [property: Required, StringLength(40)] string Code,
    [property: Required, StringLength(160)] string Name,
    Guid ProductGroupId,
    [property: Required, StringLength(30)] string SaleUnit,
    [property: Range(typeof(decimal), "0.005", "10000000000", ParseLimitsInInvariantCulture = true, ConvertValueInInvariantCulture = true)] decimal Price,
    bool IsActive = true);
public sealed record UpdateProductRequest([property: Required] ProductRequest Product, Guid Version);
public sealed record ProductResponse(Guid Id, string Code, string Name, Guid ProductGroupId,
    string SaleUnit, long PriceMinor, decimal Price, bool IsActive, Guid Version);
public sealed record ProductGroupResponse(Guid Id, string Name);
public sealed record StateResponse(Guid Id, string Code, string Name);
public sealed record CityResponse(Guid Id, Guid StateId, string Name);
public sealed record SalespersonResponse(Guid Id, string Name, bool IsActive);
public sealed record InstallmentDefinitionResponse(int Number, int Days, int BasisPoints);
public sealed record PaymentTermResponse(int Code, string Name, IReadOnlyList<InstallmentDefinitionResponse> Installments);

public sealed record OrderLineRequest(Guid ProductId,
    [property: Range(1, 1_000_000)] int Quantity);
public sealed record CreateOrderRequest(Guid CustomerId, int PaymentTermCode, DateOnly DispatchDate,
    int DispatchHour, Guid SubmissionKey,
    [property: Required, MinLength(1), MaxLength(100)] List<OrderLineRequest> Items);
public sealed record OrderItemResponse(Guid Id, Guid ProductId, string ProductName,
    int RequestedQuantity, int FulfilledQuantity, int UnfulfilledQuantity, long UnitPriceMinor);
public sealed record OrderResponse(Guid Id, Guid CustomerId, Guid SalespersonId, string CustomerName,
    int PaymentTermCode, DateOnly DispatchDate, int DispatchHour, string Status,
    DateTime SubmittedAtUtc, long RequestedTotalMinor, Guid Version, IReadOnlyList<OrderItemResponse> Items);
public sealed record SubmissionResult(OrderResponse Order, bool Created);
