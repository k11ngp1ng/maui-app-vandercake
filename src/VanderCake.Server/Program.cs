using System.Text.Json.Serialization;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using VanderCake.Contracts.Health;
using VanderCake.Infrastructure;
using VanderCake.Infrastructure.Identity;
using VanderCake.Infrastructure.Persistence;
using VanderCake.Server.Api;
using VanderCake.Server.Configuration;
using VanderCake.Domain.Finance;
using VanderCake.Server.Security;

var migrate = args.Contains("--migrate");
var seed = args.Contains("--seed-demo");
var webArgs = args.Where(x => x is not ("--migrate" or "--seed-demo")).ToArray();
var builder = WebApplication.CreateBuilder(webArgs);
builder.Configuration.AddJsonFile("appsettings.Local.json", optional: true, reloadOnChange: true);
builder.Configuration.AddEnvironmentVariables();
builder.Configuration.AddCommandLine(webArgs);

builder.Services.AddSingleton(TimeProvider.System);
var connection = builder.Configuration.GetConnectionString("VanderCake") ?? DatabaseSettings.DefaultConnectionString;
DatabaseSettings.PrepareDirectory(connection);
builder.Services.AddInfrastructure(connection);
builder.Services.AddOptions<FinanceOptions>().Bind(builder.Configuration.GetSection("Finance"))
    .ValidateDataAnnotations().ValidateOnStart();
builder.Services.AddSingleton(sp => sp.GetRequiredService<IOptions<FinanceOptions>>().Value.ToRules());
builder.Services.AddVanderCakeAuthentication();
builder.Services.AddDataProtection().SetApplicationName("VanderCake");
builder.Services.AddRazorPages(options => options.Conventions.AuthorizeFolder("/Admin", Policies.AdminWebsite));
builder.Services.AddAntiforgery(options => options.Cookie.SecurePolicy = CookieSecurePolicy.Always);
builder.Services.ConfigureHttpJsonOptions(options =>
    options.SerializerOptions.UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow);
builder.Services.Configure<RouteHandlerOptions>(options => options.ThrowOnBadRequest = true);
builder.Services.AddProblemDetails(options => options.CustomizeProblemDetails = context =>
    context.ProblemDetails.Extensions["traceId"] = context.HttpContext.TraceIdentifier);
builder.Services.AddExceptionHandler<ApiExceptionHandler>();

var app = builder.Build();
if (seed && !app.Environment.IsDevelopment())
    throw new InvalidOperationException("Demo seeding is allowed only in Development.");
if (migrate || seed)
{
    await using var scope = app.Services.CreateAsyncScope();
    var db = scope.ServiceProvider.GetRequiredService<VanderCakeDbContext>();
    if (migrate)
    {
        await db.Database.MigrateAsync();
        app.Logger.LogInformation("Database migration complete.");
    }
    if (seed)
    {
        if ((await db.Database.GetPendingMigrationsAsync()).Any())
            throw new InvalidOperationException("Apply migrations before demo seeding.");
        await scope.ServiceProvider.GetRequiredService<DemoSeeder>().SeedAsync(
            app.Configuration["Demo:AdminPassword"] ?? "",
            app.Configuration["Demo:SalespersonPassword"] ?? "", CancellationToken.None);
        if (app.Configuration.GetValue<bool>("Demo:AcademicFixture"))
            await scope.ServiceProvider.GetRequiredService<DemoSeeder>().SeedAcademicFixtureAsync(CancellationToken.None);
        app.Logger.LogInformation("Development demo seed complete. Existing credentials were preserved.");
    }
    return;
}

app.UseExceptionHandler();
app.UseStatusCodePages(async context =>
{
    var response = context.HttpContext.Response;
    await Results.Problem(statusCode: response.StatusCode, title: response.StatusCode switch
    {
        401 => "Authentication required", 403 => "Forbidden", 404 => "Not found",
        405 => "Method not allowed", _ => "Request failed"
    }).ExecuteAsync(context.HttpContext);
});
if (!app.Environment.IsDevelopment())
{
    app.UseHsts();
    app.UseHttpsRedirection();
}
// Never serve credential forms or accept API traffic over cleartext, including Development.
app.Use(async (context, next) =>
{
    if (!context.Request.IsHttps && (context.Request.Path.StartsWithSegments("/api") ||
        context.Request.Path.StartsWithSegments("/Account")))
    {
        await Results.Problem(statusCode: 400, title: "HTTPS required",
            detail: "Use the HTTPS server profile for authentication and API access.").ExecuteAsync(context);
        return;
    }
    await next(context);
});
// The administrator UI is English; HTML number inputs post invariant decimal points.
// Keep display and form binding consistent regardless of the Windows host locale.
app.UseRequestLocalization(new RequestLocalizationOptions().SetDefaultCulture("en-US")
    .AddSupportedCultures("en-US").AddSupportedUICultures("en-US"));
app.UseRouting();
app.UseAuthentication();
app.UseAuthorization();
app.MapStaticAssets();
app.MapRazorPages().WithStaticAssets();
app.MapGet("/health", () => TypedResults.Ok(new HealthResponse("healthy", "VanderCake")))
    .AllowAnonymous().WithName("Health");
app.MapDistributionApi();
app.Run();

public partial class Program;
