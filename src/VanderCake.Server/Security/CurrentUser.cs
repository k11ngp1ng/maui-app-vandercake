using System.Security.Claims;
using VanderCake.Application.Abstractions;
using VanderCake.Domain.Customers;

namespace VanderCake.Server.Security;

public sealed class CurrentUser(IHttpContextAccessor accessor) : ICurrentUser
{
    public Guid UserId => Guid.TryParse(accessor.HttpContext?.User.FindFirstValue(ClaimTypes.NameIdentifier), out var id)
        ? id : throw new ApplicationFailure(FailureKind.Forbidden, "Authenticated identity is required.");
    public bool IsAdministrator => accessor.HttpContext?.User.IsInRole(RoleNames.Administrator) == true;
}
