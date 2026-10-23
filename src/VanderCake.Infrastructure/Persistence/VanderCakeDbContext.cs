using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using VanderCake.Domain.Catalog;
using VanderCake.Domain.Common;
using VanderCake.Domain.Customers;
using VanderCake.Domain.Finance;
using VanderCake.Domain.Inventory;
using VanderCake.Domain.Orders;
using VanderCake.Infrastructure.Identity;

namespace VanderCake.Infrastructure.Persistence;

public sealed class VanderCakeDbContext(DbContextOptions<VanderCakeDbContext> options)
    : IdentityDbContext<ApplicationUser, IdentityRole<Guid>, Guid>(options)
{
    public DbSet<Salesperson> Salespersons => Set<Salesperson>();
    public DbSet<Customer> Customers => Set<Customer>();
    public DbSet<State> States => Set<State>();
    public DbSet<City> Cities => Set<City>();
    public DbSet<ProductGroup> ProductGroups => Set<ProductGroup>();
    public DbSet<Product> Products => Set<Product>();
    public DbSet<InventoryLot> InventoryLots => Set<InventoryLot>();
    public DbSet<Order> Orders => Set<Order>();
    public DbSet<AllocationBatch> AllocationBatches => Set<AllocationBatch>();
    public DbSet<OrderItem> OrderItems => Set<OrderItem>();
    public DbSet<Sale> Sales => Set<Sale>();
    public DbSet<SaleItem> SaleItems => Set<SaleItem>();
    public DbSet<PaymentTerm> PaymentTerms => Set<PaymentTerm>();
    public DbSet<InstallmentDefinition> InstallmentDefinitions => Set<InstallmentDefinition>();
    public DbSet<AccountsReceivable> AccountsReceivable => Set<AccountsReceivable>();
    public DbSet<PaymentRecord> Payments => Set<PaymentRecord>();
    public DbSet<LotConsumption> LotConsumptions => Set<LotConsumption>();

    protected override void OnModelCreating(ModelBuilder b)
    {
        base.OnModelCreating(b);
        b.ApplyConfigurationsFromAssembly(typeof(VanderCakeDbContext).Assembly);
        foreach (var entity in b.Model.GetEntityTypes().Where(x => typeof(Entity).IsAssignableFrom(x.ClrType)))
        {
            b.Entity(entity.ClrType).HasKey(nameof(Entity.Id));
            b.Entity(entity.ClrType).Property<Guid>(nameof(Entity.Version)).IsConcurrencyToken();
            foreach (var property in entity.GetProperties().Where(p => p.ClrType == typeof(string) && p.GetMaxLength() is null))
                property.SetMaxLength(160);
        }

        b.Entity<Salesperson>().HasOne<ApplicationUser>().WithOne().HasForeignKey<Salesperson>(x => x.UserId).OnDelete(DeleteBehavior.Restrict);
        b.Entity<Salesperson>().HasIndex(x => x.UserId).IsUnique();
        b.Entity<State>().Property(x => x.Code).HasMaxLength(2);
        b.Entity<State>().HasIndex(x => x.Code).IsUnique();
        b.Entity<City>().HasOne<State>().WithMany().HasForeignKey(x => x.StateId).OnDelete(DeleteBehavior.Restrict);
        b.Entity<City>().HasIndex(x => new { x.StateId, x.Name }).IsUnique();
        b.Entity<Customer>().HasOne<Salesperson>().WithMany().HasForeignKey(x => x.SalespersonId).OnDelete(DeleteBehavior.Restrict);
        b.Entity<Customer>().HasOne<City>().WithMany().HasForeignKey(x => x.CityId).OnDelete(DeleteBehavior.Restrict);
        b.Entity<Customer>().Property(x => x.Address).HasMaxLength(240);
        b.Entity<Customer>().Property(x => x.Phone).HasMaxLength(30);
        b.Entity<Customer>().HasIndex(x => new { x.SalespersonId, x.VisitOrder });
        b.Entity<Customer>().ToTable(t => t.HasCheckConstraint("CK_Customer_VisitOrder", "VisitOrder > 0"));
        b.Entity<ProductGroup>().HasIndex(x => x.Name).IsUnique();
        b.Entity<ProductGroup>().Property(x => x.Name).HasMaxLength(80);
        b.Entity<Product>().HasOne<ProductGroup>().WithMany().HasForeignKey(x => x.ProductGroupId).OnDelete(DeleteBehavior.Restrict);
        b.Entity<Product>().Property(x => x.Code).HasMaxLength(40);
        b.Entity<Product>().Property(x => x.SaleUnit).HasMaxLength(30);
        b.Entity<Product>().HasIndex(x => x.Code).IsUnique();
        b.Entity<Product>().ToTable(t => t.HasCheckConstraint("CK_Product_Price", "PriceMinor > 0 AND PriceMinor <= 1000000000000"));
        b.Entity<InventoryLot>().HasOne<Product>().WithMany().HasForeignKey(x => x.ProductId).OnDelete(DeleteBehavior.Restrict);
        b.Entity<InventoryLot>().Property(x => x.Code).HasMaxLength(60);
        b.Entity<InventoryLot>().HasIndex(x => new { x.ProductId, x.Code }).IsUnique();
        b.Entity<InventoryLot>().HasIndex(x => new { x.ProductId, x.ExpirationDate });
        b.Entity<InventoryLot>().ToTable(t =>
        {
            t.HasCheckConstraint("CK_Lot_Quantity", "ReceivedQuantity > 0 AND ReceivedQuantity <= 1000000 AND AvailableQuantity >= 0 AND AvailableQuantity <= ReceivedQuantity");
            t.HasCheckConstraint("CK_Lot_Dates", "ReceivedDate > '0001-01-01' AND ExpirationDate >= ReceivedDate");
        });

        b.Entity<PaymentTerm>().HasKey(x => x.Code);
        b.Entity<PaymentTerm>().Property(x => x.Code).ValueGeneratedNever();
        b.Entity<PaymentTerm>().Property(x => x.Name).HasMaxLength(80);
        b.Entity<PaymentTerm>().HasMany(x => x.Definitions).WithOne().HasForeignKey(x => x.PaymentTermCode).OnDelete(DeleteBehavior.Restrict);
        b.Entity<PaymentTerm>().Navigation(x => x.Definitions).UsePropertyAccessMode(PropertyAccessMode.Field);
        b.Entity<InstallmentDefinition>().HasKey(x => new { x.PaymentTermCode, x.Number });
        b.Entity<InstallmentDefinition>().ToTable(t => t.HasCheckConstraint("CK_Definition", "Number > 0 AND Days BETWEEN 1 AND 365 AND BasisPoints BETWEEN 1 AND 10000"));

        b.Entity<Order>().HasOne<Customer>().WithMany().HasForeignKey(x => x.CustomerId).OnDelete(DeleteBehavior.Restrict);
        b.Entity<AllocationBatch>().HasOne<ApplicationUser>().WithMany().HasForeignKey(x => x.AdministratorId).OnDelete(DeleteBehavior.Restrict);
        b.Entity<AllocationBatch>().HasIndex(x => x.RequestKey).IsUnique();
        b.Entity<AllocationBatch>().Property(x => x.RequestFingerprint).HasMaxLength(64);
        b.Entity<Order>().HasOne<AllocationBatch>().WithMany().HasForeignKey(x => x.AllocationBatchId).OnDelete(DeleteBehavior.Restrict);
        b.Entity<Order>().HasOne<Salesperson>().WithMany().HasForeignKey(x => x.SalespersonId).OnDelete(DeleteBehavior.Restrict);
        b.Entity<Order>().HasOne<PaymentTerm>().WithMany().HasForeignKey(x => x.PaymentTermCode).OnDelete(DeleteBehavior.Restrict);
        b.Entity<Order>().HasMany(x => x.Items).WithOne().HasForeignKey(x => x.OrderId).OnDelete(DeleteBehavior.Restrict);
        b.Entity<Order>().Navigation(x => x.Items).UsePropertyAccessMode(PropertyAccessMode.Field);
        b.Entity<Order>().Ignore(x => x.RequestedTotalMinor);
        b.Entity<Order>().Property(x => x.RequestFingerprint).HasMaxLength(64);
        b.Entity<Order>().HasIndex(x => new { x.SalespersonId, x.SubmissionKey }).IsUnique();
        b.Entity<Order>().HasIndex(x => new { x.Status, x.DispatchDate });
        b.Entity<Order>().ToTable(t =>
        {
            t.HasCheckConstraint("CK_Order_State", "Status IN (0, 1, 2, 3)");
            t.HasCheckConstraint("CK_Order_Dispatch", "DispatchWindow IN (12, 19) AND DispatchDate > '0001-01-01'");
        });
        b.Entity<OrderItem>().HasOne<Product>().WithMany().HasForeignKey(x => x.ProductId).OnDelete(DeleteBehavior.Restrict);
        b.Entity<OrderItem>().HasIndex(x => new { x.OrderId, x.ProductId }).IsUnique();
        b.Entity<OrderItem>().Ignore(x => x.RequestedTotalMinor);
        b.Entity<OrderItem>().ToTable(t =>
        {
            t.HasCheckConstraint("CK_OrderItem_Quantity", "RequestedQuantity BETWEEN 1 AND 1000000 AND FulfilledQuantity >= 0 AND FulfilledQuantity <= RequestedQuantity");
            t.HasCheckConstraint("CK_OrderItem_Price", "UnitPriceMinor > 0 AND UnitPriceMinor * RequestedQuantity <= 1000000000000");
        });

        b.Entity<Sale>().HasOne<Order>().WithOne().HasForeignKey<Sale>(x => x.OrderId).OnDelete(DeleteBehavior.Restrict);
        b.Entity<Sale>().HasOne<Customer>().WithMany().HasForeignKey(x => x.CustomerId).OnDelete(DeleteBehavior.Restrict);
        b.Entity<Sale>().HasOne<Salesperson>().WithMany().HasForeignKey(x => x.SalespersonId).OnDelete(DeleteBehavior.Restrict);
        b.Entity<Sale>().HasOne<PaymentTerm>().WithMany().HasForeignKey(x => x.PaymentTermCode).OnDelete(DeleteBehavior.Restrict);
        b.Entity<Sale>().HasMany(x => x.Items).WithOne().HasForeignKey(x => x.SaleId).OnDelete(DeleteBehavior.Restrict);
        b.Entity<Sale>().Navigation(x => x.Items).UsePropertyAccessMode(PropertyAccessMode.Field);
        b.Entity<Sale>().ToTable(t => t.HasCheckConstraint("CK_Sale_Total", "TotalMinor > 0 AND TotalMinor <= 1000000000000 AND SaleDate > '0001-01-01'"));
        b.Entity<SaleItem>().HasOne<Product>().WithMany().HasForeignKey(x => x.ProductId).OnDelete(DeleteBehavior.Restrict);
        b.Entity<SaleItem>().HasOne<OrderItem>().WithOne().HasForeignKey<SaleItem>(x => x.OrderItemId).OnDelete(DeleteBehavior.Restrict);
        b.Entity<SaleItem>().ToTable(t => t.HasCheckConstraint("CK_SaleItem", "Quantity BETWEEN 1 AND 1000000 AND UnitPriceMinor > 0 AND TotalMinor = UnitPriceMinor * Quantity AND TotalMinor <= 1000000000000"));

        b.Entity<AccountsReceivable>().HasOne<Sale>().WithMany().HasForeignKey(x => x.SaleId).OnDelete(DeleteBehavior.Restrict);
        b.Entity<AccountsReceivable>().HasIndex(x => new { x.SaleId, x.Number }).IsUnique();
        b.Entity<AccountsReceivable>().HasIndex(x => new { x.Status, x.DueDate });
        b.Entity<AccountsReceivable>().ToTable(t =>
        {
            t.HasCheckConstraint("CK_Receivable", "PrincipalMinor > 0 AND PrincipalMinor <= 1000000000000 AND Number > 0 AND DueDate > '0001-01-01' AND Status IN (0, 1)");
            t.HasCheckConstraint("CK_Receivable_Rates", "PercentageBasisPoints BETWEEN 1 AND 10000 AND PenaltyBasisPoints BETWEEN 0 AND 10000 AND DailyInterestBasisPoints BETWEEN 0 AND 10000 AND DailyDiscountBasisPoints BETWEEN 0 AND 10000");
        });
        b.Entity<PaymentRecord>().HasOne<AccountsReceivable>().WithOne().HasForeignKey<PaymentRecord>(x => x.AccountsReceivableId).OnDelete(DeleteBehavior.Restrict);
        b.Entity<PaymentRecord>().HasOne<ApplicationUser>().WithMany().HasForeignKey(x => x.AdministratorId).OnDelete(DeleteBehavior.Restrict);
        b.Entity<PaymentRecord>().HasIndex(x => x.RequestKey).IsUnique();
        b.Entity<PaymentRecord>().ToTable(t => t.HasCheckConstraint("CK_Payment", "PrincipalMinor > 0 AND PenaltyMinor >= 0 AND InterestMinor >= 0 AND DiscountMinor >= 0 AND DiscountMinor <= PrincipalMinor AND PaidMinor = PrincipalMinor + PenaltyMinor + InterestMinor - DiscountMinor AND PaidMinor >= 0 AND PaymentDate > '0001-01-01'"));
        b.Entity<LotConsumption>().HasOne<OrderItem>().WithMany().HasForeignKey(x => x.OrderItemId).OnDelete(DeleteBehavior.Restrict);
        b.Entity<LotConsumption>().HasOne<InventoryLot>().WithMany().HasForeignKey(x => x.InventoryLotId).OnDelete(DeleteBehavior.Restrict);
        b.Entity<LotConsumption>().HasOne<ApplicationUser>().WithMany().HasForeignKey(x => x.AdministratorId).OnDelete(DeleteBehavior.Restrict);
        b.Entity<LotConsumption>().HasIndex(x => new { x.OrderItemId, x.InventoryLotId }).IsUnique();
        b.Entity<LotConsumption>().ToTable(t => t.HasCheckConstraint("CK_Consumption", "Quantity BETWEEN 1 AND 1000000"));
        ReferenceSeed.Configure(b);
    }

    public override Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        foreach (var entry in ChangeTracker.Entries<Order>().Where(e => e.State == EntityState.Added))
            if (entry.Entity.Items.Count == 0) throw new DomainRuleException("An order requires at least one item.");
        return base.SaveChangesAsync(cancellationToken);
    }
}
