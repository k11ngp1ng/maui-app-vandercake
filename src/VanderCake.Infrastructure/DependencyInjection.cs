using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using VanderCake.Application.Abstractions;
using VanderCake.Application.Services;
using VanderCake.Infrastructure.Identity;
using VanderCake.Infrastructure.Persistence;

namespace VanderCake.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, string connectionString)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(connectionString);
        services.AddDbContext<VanderCakeDbContext>(options => options.UseSqlite(connectionString, sqlite => sqlite.CommandTimeout(5)));
        services.AddIdentityCore<ApplicationUser>(options =>
        {
            options.User.RequireUniqueEmail = true;
            options.Password.RequiredLength = 12;
            options.Lockout.MaxFailedAccessAttempts = 5;
            options.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(5);
        }).AddRoles<IdentityRole<Guid>>().AddEntityFrameworkStores<VanderCakeDbContext>()
          .AddSignInManager().AddDefaultTokenProviders();
        services.AddScoped<IDistributionStore, DistributionStore>();
        services.AddScoped<IReferenceDataService, ReferenceDataService>();
        services.AddScoped<AccessScope>();
        services.AddScoped<ICustomerService, CustomerService>();
        services.AddScoped<IProductService, ProductService>();
        services.AddScoped<IOrderService, OrderService>();
        services.AddSingleton<WorkflowWriteCoordinator>();
        services.AddSingleton<BusinessClock>();
        services.AddScoped<IWorkflowStore, WorkflowStore>();
        services.AddScoped<IFulfillmentService, FulfillmentService>();
        services.AddScoped<IFinanceService, FinanceService>();
        services.AddScoped<DemoSeeder>();
        services.AddScoped<IAdministrationStore, AdministrationStore>();
        services.AddScoped<IAdministrationService, AdministrationService>();
        return services;
    }
}
