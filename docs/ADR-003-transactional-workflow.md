# ADR 003 — Atomic allocation and installment settlement

Date: 2026-10-21, America/Sao_Paulo. Status: implemented in Phase 03.

## Decisions
Keep Domain/Application/Contracts independent of EF and HTTP. IFulfillmentService and IFinanceService own the use cases; IWorkflowStore and IWorkflowTransaction are the persistence ports. Infrastructure implements real SQLite transactions. Razor handlers and HTTP endpoints invoke the same services.

An allocation request contains explicit administrator-approved quantities for every selected order line. The server never derives a customer allocation or priority. Batch size is limited to 100 orders, each retaining the existing 100-line limit. Server reads current order versions and inventory, validates dispatch/quantity bounds, chooses eligible lots by expiry then lot ID, preserves shortages and finalizes all selected orders atomically.

AllocationBatch adds a globally unique request key, canonical payload hash, administrator ID and UTC timestamp. Orders link to their batch; immutable requested/fulfilled quantities and LotConsumption rows explain approved quantities and actual lots. A zero-fulfilled order is still audited and becomes Cut, with no sale or receivable. Identical batch retry returns the original result; changed content/key conflicts never duplicate effects.

## Transaction boundary and concurrency
A singleton async semaphore serializes allocation/payment operations within one server process. Inside that gate, clear prior tracked read state and begin a Serializable, non-deferred SQLite transaction before reading mutable workflow state. Microsoft.Data.Sqlite acquires the writer reservation at transaction start. Guid concurrency versions and database unique/CHECK/FK constraints remain additional safeguards.

Allocation flushes stock/order/audit changes, then creates and flushes fulfilled sales and installments, then commits. Payment flushes the installment's paid state, inserts payment history, then commits. Any exception before commit rolls back both flushes. Fault-injection tests read already-written state inside the transaction and then verify rollback from a fresh context.

No application retry silently reinterprets stale allocations. Stale versions/finalized orders return 409. Insufficient approved stock or invalid quantities return 400. SQLite busy/locked errors are bounded by a five-second workflow timeout and mapped to 409. A client must reload before making a new decision; identical idempotent retries remain safe.

## Finance
Sale date is server business date in America/Sao_Paulo, never a submitted client date. Sales use fulfilled units and original server price snapshots. Immutable terms 10/20 generate due dates and exact cent totals; final installment receives the remainder.

Finance configuration uses integer basis points: 500=5% one-time penalty, 200=2% daily simple interest, 100=1% daily early discount. FinanceOptions validates each rate in 0..10000 at startup. Restart after changing settings; new receivables snapshot the new rates, while existing receivables retain theirs.

Persist only Open/Paid. Derive Open/Overdue/Paid at read time from business date and due date. There is no scheduled job. Balance exposes open original principal, overdue principal and current settlement total separately. Paid installments contribute zero outstanding principal/payable while retaining the actual receipt components.

Full settlement only: the server quotes and recomputes principal/penalty/interest/discount; a submitted quote date, version and amount are assertions, not authoritative amounts or backdating controls. Reject stale quotes and mismatched amounts. Discount is capped at principal. Zero-payable settlement requires explicit confirmation. One payment per installment and a unique request key prevent duplicate history; matching retries return the saved receipt.

## Ownership and UI
Administrator bearer policies protect allocation/payment mutation APIs; administrator cookie + antiforgery protect the basic workflow pages. Salespeople can read purchase history, balances, sales, installments and receipts only through current customer ownership. Orders still require original authorship AND current portfolio assignment.

Basic Razor forms support one-order allocation and full-installment payment. The API also supports an atomic multi-order batch. No complex master-management UI or native client integration is added in this phase.

## Limitations
This is a single-server, single SQLite file academic MVP. The semaphore is process-local, not a distributed lock. SQLite still has one writer; long writes and external writers may cause contention. Multiple server instances, sustained-load behavior and crash/power-loss recovery were not tested and are not deployment claims. Microsoft.Data.Sqlite's async API does not make the underlying SQLite engine fully asynchronous.

Runtime workflow transactions were tested for atomic rollback. Separately, the additive audit migration requires SQLite to rebuild Orders to add a foreign key. EF emitted a warning that PRAGMA foreign_keys=0 cannot execute in a transaction, so schema migration is not guaranteed all-or-nothing if interrupted. Apply schema upgrades during maintenance with the app stopped and a consistent backup. The tested upgrade preserved legacy orders, items and stock; forced interruption during migration is NOT RUN.

Partial payments, reversals/refunds and backorders remain P1. No automatic dispatch/allocation/interest/payment jobs exist.

## Sources checked
- [SQLite transactions](https://learn.microsoft.com/en-us/dotnet/standard/data/sqlite/transactions)
- [SQLite busy/locked handling](https://learn.microsoft.com/en-us/dotnet/standard/data/sqlite/database-errors)
- [EF Core transactions](https://learn.microsoft.com/en-us/ef/core/saving/transactions)
- [EF Core concurrency](https://learn.microsoft.com/en-us/ef/core/saving/concurrency)
