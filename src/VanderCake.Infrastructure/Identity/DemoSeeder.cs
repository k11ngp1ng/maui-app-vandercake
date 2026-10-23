using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using VanderCake.Domain.Catalog;
using VanderCake.Domain.Customers;
using VanderCake.Domain.Inventory;
using VanderCake.Infrastructure.Persistence;

namespace VanderCake.Infrastructure.Identity;

public sealed class DemoSeeder(VanderCakeDbContext db, UserManager<ApplicationUser> users,
    RoleManager<IdentityRole<Guid>> roles, TimeProvider clock)
{
    public async Task SeedAsync(string adminPassword, string salespersonPassword, CancellationToken ct)
    {
        // Callers must enforce Development and explicit opt-in. No password default exists.
        if (string.IsNullOrWhiteSpace(adminPassword) || string.IsNullOrWhiteSpace(salespersonPassword))
            throw new InvalidOperationException("Demo:AdminPassword and Demo:SalespersonPassword must be supplied through secrets/environment.");
        foreach (var role in new[] { RoleNames.Administrator, RoleNames.Salesperson })
            if (!await roles.RoleExistsAsync(role)) Check(await roles.CreateAsync(new IdentityRole<Guid>(role)));
        await UserAsync("admin@vandercake.local", adminPassword, RoleNames.Administrator);
        var seller = await UserAsync("salesperson@vandercake.local", salespersonPassword, RoleNames.Salesperson);
        var profile = await db.Salespersons.SingleOrDefaultAsync(x => x.UserId == seller.Id, ct);
        if (profile is null)
        {
            profile = new Salesperson(seller.Id, "Demo Salesperson");
            db.Salespersons.Add(profile); await db.SaveChangesAsync(ct);
        }
        if (!await db.Customers.AnyAsync(x => x.SalespersonId == profile.Id, ct))
        {
            db.Customers.AddRange(
                new Customer("Bakery A", profile.Id, ReferenceSeed.SaoPauloCityId, 1, "Demo Street 10", "11900000001"),
                new Customer("Bakery B", profile.Id, ReferenceSeed.CampinasCityId, 2, "Demo Street 20", "11900000002"),
                new Customer("Bakery C", profile.Id, ReferenceSeed.SaoPauloCityId, 3, "Demo Street 30", "11900000003"));
        }
        var definitions = new[] { ("Yeast", "YEAST", "Fresh yeast", 1250L),
            ("Cake mixes", "CAKE", "Cake mix", 2500L), ("Savory mixes", "SAVORY", "Savory mix", 2200L),
            ("Margarine", "MARG", "Bakery margarine", 1800L) };
        var today = DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(clock.GetUtcNow(),
            TimeZoneInfo.FindSystemTimeZoneById("America/Sao_Paulo")).DateTime);
        foreach (var (groupName, code, name, price) in definitions)
        {
            var group = await db.ProductGroups.SingleOrDefaultAsync(x => x.Name == groupName, ct);
            if (group is null) { group = new ProductGroup(groupName); db.ProductGroups.Add(group); }
            var product = await db.Products.SingleOrDefaultAsync(x => x.Code == code, ct);
            if (product is null)
            {
                product = new Product(code, name, group.Id, "package", price);
                db.Products.Add(product);
                db.InventoryLots.AddRange(
                    new InventoryLot(product.Id, "DEMO-EARLY", today, today.AddDays(7), 6),
                    new InventoryLot(product.Id, "DEMO-LATER", today, today.AddDays(30), 9),
                    new InventoryLot(product.Id, "DEMO-EXPIRED", today.AddDays(-10), today.AddDays(-1), 3));
            }
        }
        await db.SaveChangesAsync(ct);
    }

    public async Task SeedAcademicFixtureAsync(CancellationToken ct)
    {
        // Explicit Development seed only. Never replenish consumed lots or reset existing data.
        var group = await db.ProductGroups.FirstAsync(ct);
        var today = DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(clock.GetUtcNow(),
            TimeZoneInfo.FindSystemTimeZoneById("America/Sao_Paulo")).DateTime);
        foreach (var (code, name, price, quantity) in new[] {
            ("XYZ", "Academic XYZ", 1000L, 15), ("EX235", "BRL 235 example", 23500L, 1) })
        {
            if (await db.Products.AnyAsync(p => p.Code == code, ct)) continue;
            var product = new Product(code, name, group.Id, "package", price);
            db.Products.Add(product);
            if (code == "XYZ")
                db.InventoryLots.AddRange(
                    new InventoryLot(product.Id, "ACADEMIC-EARLY", today, today.AddDays(7), 6),
                    new InventoryLot(product.Id, "ACADEMIC-LATER", today, today.AddDays(30), 9));
            else db.InventoryLots.Add(new InventoryLot(product.Id, "ACADEMIC-235", today, today.AddDays(30), quantity));
        }
        await db.SaveChangesAsync(ct);
    }

    private async Task<ApplicationUser> UserAsync(string email, string password, string role)
    {
        var user = await users.FindByEmailAsync(email);
        if (user is null)
        {
            user = new ApplicationUser { Id = Guid.NewGuid(), UserName = email, Email = email, EmailConfirmed = true };
            Check(await users.CreateAsync(user, password));
        }
        if (!await users.IsInRoleAsync(user, role)) Check(await users.AddToRoleAsync(user, role));
        return user;
    }
    private static void Check(IdentityResult result)
    {
        if (!result.Succeeded)
            throw new InvalidOperationException("Demo identity setup failed: " + string.Join("; ", result.Errors.Select(x => x.Description)));
    }
}
