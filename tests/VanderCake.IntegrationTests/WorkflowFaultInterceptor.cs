using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.EntityFrameworkCore.Storage;
using VanderCake.Domain.Finance;

namespace VanderCake.IntegrationTests;

// Test-only database fault injection, with SQL evidence that an earlier flush really wrote rows.
public sealed class WorkflowFaultInterceptor : SaveChangesInterceptor
{
    public bool FailFinance { get; set; }
    public bool FailPayment { get; set; }
    public bool SawInventoryFlush { get; private set; }
    public bool SawPaidFlush { get; private set; }

    public override ValueTask<InterceptionResult<int>> SavingChangesAsync(DbContextEventData eventData,
        InterceptionResult<int> result, CancellationToken cancellationToken = default)
    {
        var db = eventData.Context!;
        if (FailFinance && db.ChangeTracker.Entries<AccountsReceivable>().Any(e => e.State == EntityState.Added))
            throw new InvalidOperationException("Injected failure while creating receivables.");
        if (FailPayment && db.ChangeTracker.Entries<PaymentRecord>().Any(e => e.State == EntityState.Added))
            throw new InvalidOperationException("Injected failure while inserting payment history.");
        return ValueTask.FromResult(result);
    }

    public override async ValueTask<int> SavedChangesAsync(SaveChangesCompletedEventData eventData, int result,
        CancellationToken cancellationToken = default)
    {
        var db = eventData.Context!;
        if ((FailFinance || FailPayment) && db.Database.CurrentTransaction is not null)
        {
            await using var command = db.Database.GetDbConnection().CreateCommand();
            command.Transaction = db.Database.CurrentTransaction.GetDbTransaction();
            if (FailFinance)
            {
                command.CommandText = "SELECT COUNT(*) FROM Orders WHERE Status <> 0";
                SawInventoryFlush |= Convert.ToInt64(await command.ExecuteScalarAsync(cancellationToken)) > 0;
            }
            if (FailPayment)
            {
                command.CommandText = "SELECT COUNT(*) FROM AccountsReceivable WHERE Status = 1";
                SawPaidFlush |= Convert.ToInt64(await command.ExecuteScalarAsync(cancellationToken)) > 0;
            }
        }
        return result;
    }
}
