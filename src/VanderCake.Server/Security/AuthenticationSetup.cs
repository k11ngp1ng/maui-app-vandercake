using Microsoft.AspNetCore.Authentication.BearerToken;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Options;
using VanderCake.Application.Abstractions;
using VanderCake.Domain.Customers;
using VanderCake.Infrastructure.Identity;

namespace VanderCake.Server.Security;

public static class Policies
{
    public const string ApiUser = "ApiUser";
    public const string ApiAdmin = "ApiAdmin";
    public const string ApiSalesperson = "ApiSalesperson";
    public const string AdminWebsite = "AdminWebsite";
}

public sealed class ActiveIdentityRequirement : IAuthorizationRequirement;

public sealed class ActiveIdentityHandler(UserManager<ApplicationUser> users, IOptions<IdentityOptions> options)
    : AuthorizationHandler<ActiveIdentityRequirement>
{
    protected override async Task HandleRequirementAsync(AuthorizationHandlerContext context, ActiveIdentityRequirement requirement)
    {
        var user = await users.GetUserAsync(context.User);
        if (user is null || !user.IsActive) return;
        var stamp = context.User.FindFirst(options.Value.ClaimsIdentity.SecurityStampClaimType)?.Value;
        if (stamp != await users.GetSecurityStampAsync(user)) return;
        var currentRoles = (await users.GetRolesAsync(user)).OrderBy(x => x).ToArray();
        var tokenRoles = context.User.FindAll(options.Value.ClaimsIdentity.RoleClaimType)
            .Select(x => x.Value).OrderBy(x => x).ToArray();
        if (!currentRoles.SequenceEqual(tokenRoles)) return;
        context.Succeed(requirement);
    }
}

public static class AuthenticationSetup
{
    public static IServiceCollection AddVanderCakeAuthentication(this IServiceCollection services)
    {
        services.AddHttpContextAccessor();
        services.AddScoped<ICurrentUser, CurrentUser>();
        services.AddScoped<IAuthorizationHandler, ActiveIdentityHandler>();
        services.AddAuthentication(options =>
        {
            options.DefaultAuthenticateScheme = IdentityConstants.ApplicationScheme;
            options.DefaultChallengeScheme = IdentityConstants.ApplicationScheme;
            options.DefaultSignInScheme = IdentityConstants.ApplicationScheme;
        })
        .AddCookie(IdentityConstants.ApplicationScheme, options =>
        {
            options.LoginPath = "/Account/Login";
            options.AccessDeniedPath = "/Account/AccessDenied";
            options.Cookie.Name = "VanderCake.Admin";
            options.Cookie.HttpOnly = true;
            options.Cookie.SecurePolicy = CookieSecurePolicy.Always;
            options.Cookie.SameSite = SameSiteMode.Lax;
            options.ExpireTimeSpan = TimeSpan.FromHours(2);
            options.SlidingExpiration = true;
            options.Events.OnValidatePrincipal = async context =>
            {
                await SecurityStampValidator.ValidatePrincipalAsync(context);
                var manager = context.HttpContext.RequestServices.GetRequiredService<UserManager<ApplicationUser>>();
                if (context.Principal is null) return;
                var user = await manager.GetUserAsync(context.Principal);
                if (user is null || !user.IsActive || !await manager.IsInRoleAsync(user, RoleNames.Administrator))
                    context.RejectPrincipal();
            };
        })
        .AddBearerToken(IdentityConstants.BearerScheme, options =>
        {
            options.BearerTokenExpiration = TimeSpan.FromMinutes(30);
            options.RefreshTokenExpiration = TimeSpan.FromMinutes(30);
        });
        services.Configure<SecurityStampValidatorOptions>(options => options.ValidationInterval = TimeSpan.Zero);
        services.AddAuthorization(options =>
        {
            void Api(string name, string[] roles) => options.AddPolicy(name, policy =>
                policy.AddAuthenticationSchemes(IdentityConstants.BearerScheme).RequireAuthenticatedUser()
                    .RequireRole(roles).AddRequirements(new ActiveIdentityRequirement()));
            Api(Policies.ApiUser, [RoleNames.Administrator, RoleNames.Salesperson]);
            Api(Policies.ApiAdmin, [RoleNames.Administrator]);
            Api(Policies.ApiSalesperson, [RoleNames.Salesperson]);
            options.AddPolicy(Policies.AdminWebsite, policy => policy
                .AddAuthenticationSchemes(IdentityConstants.ApplicationScheme).RequireAuthenticatedUser()
                .RequireRole(RoleNames.Administrator).AddRequirements(new ActiveIdentityRequirement()));
        });
        return services;
    }
}
