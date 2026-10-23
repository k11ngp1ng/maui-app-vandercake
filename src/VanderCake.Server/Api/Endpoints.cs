using Microsoft.AspNetCore.Identity;
using VanderCake.Application.Abstractions;
using VanderCake.Contracts.Api;
using VanderCake.Infrastructure.Identity;
using VanderCake.Server.Security;

namespace VanderCake.Server.Api;

public static class Endpoints
{
    public static void MapDistributionApi(this WebApplication app)
    {
        var api = app.MapGroup("/api").AddEndpointFilter<RequestValidationFilter>();
        api.MapWorkflowEndpoints();
        api.MapPost("/auth/login", async (LoginRequest request, UserManager<ApplicationUser> users,
            SignInManager<ApplicationUser> signIn) =>
        {
            var user = await users.FindByEmailAsync(request.Email);
            if (user is null || !user.IsActive)
                return Results.Problem(statusCode: 401, title: "Invalid credentials");
            var result = await signIn.CheckPasswordSignInAsync(user, request.Password, lockoutOnFailure: true);
            if (!result.Succeeded || (await users.GetRolesAsync(user)).Count == 0)
                return Results.Problem(statusCode: 401, title: "Invalid credentials");
            var principal = await signIn.CreateUserPrincipalAsync(user);
            return Results.SignIn(principal, authenticationScheme: IdentityConstants.BearerScheme);
        }).AllowAnonymous();

        api.MapGet("/auth/me", async (HttpContext context, UserManager<ApplicationUser> users) =>
        {
            var user = (await users.GetUserAsync(context.User))!;
            return new UserResponse(user.Id, user.Email!, (await users.GetRolesAsync(user)).ToArray());
        }).RequireAuthorization(Policies.ApiUser);
        api.MapPost("/auth/logout", async (HttpContext context, UserManager<ApplicationUser> users) =>
        {
            var user = (await users.GetUserAsync(context.User))!;
            await users.UpdateSecurityStampAsync(user);
            return Results.NoContent();
        }).RequireAuthorization(Policies.ApiUser);

        var customers = api.MapGroup("/customers").RequireAuthorization(Policies.ApiUser);
        customers.MapGet("/", (ICustomerService service, CancellationToken ct) => service.ListAsync(ct));
        customers.MapGet("/{id:guid}", (Guid id, ICustomerService service, CancellationToken ct) => service.GetAsync(id, ct));
        customers.MapPost("/", async (CustomerRequest request, ICustomerService service, CancellationToken ct) =>
        {
            var result = await service.CreateAsync(request, ct);
            return Results.Created($"/api/customers/{result.Id}", result);
        });
        customers.MapPut("/{id:guid}", (Guid id, UpdateCustomerRequest request, ICustomerService service, CancellationToken ct)
            => service.UpdateAsync(id, request, ct));

        var products = api.MapGroup("/products").RequireAuthorization(Policies.ApiUser);
        products.MapGet("/", (IProductService service, CancellationToken ct) => service.ListAsync(ct));
        products.MapGet("/{id:guid}", (Guid id, IProductService service, CancellationToken ct) => service.GetAsync(id, ct));
        products.MapPost("/", async (ProductRequest request, IProductService service, CancellationToken ct) =>
        {
            var result = await service.CreateAsync(request, ct);
            return Results.Created($"/api/products/{result.Id}", result);
        }).RequireAuthorization(Policies.ApiAdmin);
        products.MapPut("/{id:guid}", (Guid id, UpdateProductRequest request, IProductService service, CancellationToken ct)
            => service.UpdateAsync(id, request, ct)).RequireAuthorization(Policies.ApiAdmin);

        var orders = api.MapGroup("/orders").RequireAuthorization(Policies.ApiUser);
        orders.MapGet("/", (IOrderService service, CancellationToken ct) => service.ListAsync(ct));
        orders.MapGet("/{id:guid}", (Guid id, IOrderService service, CancellationToken ct) => service.GetAsync(id, ct));
        orders.MapPost("/", async (CreateOrderRequest request, IOrderService service, CancellationToken ct) =>
        {
            var result = await service.SubmitAsync(request, ct);
            return result.Created ? Results.Created($"/api/orders/{result.Order.Id}", result.Order)
                : Results.Ok(result.Order);
        }).RequireAuthorization(Policies.ApiSalesperson);

        var references = api.MapGroup("/reference").RequireAuthorization(Policies.ApiUser);
        references.MapGet("/states", (IReferenceDataService s, CancellationToken ct) => s.StatesAsync(ct));
        references.MapGet("/cities", (IReferenceDataService s, CancellationToken ct) => s.CitiesAsync(ct));
        references.MapGet("/product-groups", (IReferenceDataService s, CancellationToken ct) => s.ProductGroupsAsync(ct));
        references.MapGet("/payment-terms", (IReferenceDataService s, CancellationToken ct) => s.PaymentTermsAsync(ct));
        references.MapGet("/salespersons", (IReferenceDataService s, CancellationToken ct) => s.SalespersonsAsync(ct))
            .RequireAuthorization(Policies.ApiAdmin);
    }
}
