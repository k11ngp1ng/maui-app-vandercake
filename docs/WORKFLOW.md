# Phase 03 workflow and API guide

Use the HTTPS server profile and development credentials configured through DEVELOPMENT.md.
Money fields ending Minor are BRL cents. Prices, sale/payment dates, finance rates and derived amounts are server-controlled.

## Administrator pages
- /Admin/Orders — submitted orders, requested quantities, dispatch and allocation link.
- /Admin/Allocation/{orderId} — enter approved quantities and, if necessary, a future dispatch date/window; review lot stock and confirm.
- /Admin/AllocationHistory/{batchId} — final statuses, shortages, sale/installments and consumed lots with actor/time.
- /Admin/Receivables — Open/Overdue/Paid installments and payable amounts.
- /Admin/Payment/{receivableId} — confirm current full settlement; paid records show receipt history.

Forms use secure cookie authentication and antiforgery. The page supports one order at a time; the API below supports several in one transaction. Entering zero for every line deliberately closes the order as Cut. There is no automatic backorder.

## Read submitted orders and stock (administrator bearer)
GET /api/admin/orders/submitted
GET /api/admin/stock?dispatchDate=2026-10-22

Use a real current/future planning date. Stock separates physical, eligible and already expired quantities; each lot includes expiry, available units and eligibility for the selected dispatch date. Eligibility also excludes unreceived future lots.

## Confirm a manual batch
POST /api/admin/allocations/confirm
```json
{
  "requestKey": "<new GUID>",
  "orders": [
    {
      "orderId": "<submitted order GUID>",
      "version": "<version returned by GET order>",
      "items": [
        { "orderItemId": "<order line GUID>", "fulfilledQuantity": 4 }
      ]
    }
  ]
}
```
Include every line once, including zero-approved lines. Add orders to the array for an atomic batch. Optional dispatchDate and dispatchHour must be supplied together; the administrator can reschedule an elapsed window to a future date and 12 or 19 hour. Dates are America/Sao_Paulo calendar dates.

201 + Location on first success; 200 same result on identical key/content retry. 409 for changed-key content or repeated finalization/stale version. 400 for negative/over-request/over-stock quantities, expired/ineligible-only inventory or invalid dispatch. A failed batch has no effects.

The response contains the auditable batch and each order's requested/fulfilled/unfulfilled quantities, sale total, installment IDs/dates/amounts and actual lot movements. Read again with GET /api/admin/allocations/{batchId}. Sale generation and stock consumption are part of the same transaction.

## Sales, receivables, purchase history and debt
Authenticated administrator or authorized salesperson:
- GET /api/customers/{id}/purchases
- GET /api/customers/{id}/balance
- GET /api/sales/{id}
- GET /api/receivables?customerId={optional GUID}
- GET /api/receivables/{id}
- GET /api/payments/{id}

Other portfolios receive 404. Customer history/debt follows current assignment, including older sales. No arbitrary as-of date is accepted; quote/balance date comes from the server.

Balance contains OpenPrincipalMinor, OverduePrincipalMinor, PayableMinor and all installment states/history. These have different meanings: original open principal versus current discounted/charged settlement totals. Paid installments remain in history but contribute zero debt.

## Register full payment (administrator bearer)
Read the current installment quote, then:
POST /api/admin/receivables/{id}/payments
```json
{
  "requestKey": "<new GUID>",
  "version": "<version from installment quote>",
  "quoteDate": "<server quote yyyy-MM-dd>",
  "amountMinor": 11750,
  "confirmZero": false
}
```
The server rechecks version/date and computes the amount again. No underpayment, overpayment, partial payment or backdating. Explicit confirmZero=true is required when the capped early discount makes the amount zero.

201 + receipt Location on success. 200 same receipt on identical semantic retry. Reusing a key for another installment/date/amount conflicts, as does a new key for an already paid installment. A matching retry's old version is allowed because it returns the existing payment without another write. A stale quote returns 409; amount mismatch returns 400.

Payment history stores original principal, actual paid amount, penalty, simple interest, discount, administrator and UTC record time. All finance writes are atomic.

## Configuration
GET /api/admin/finance/rules shows the active rules.
Set configuration (and restart) with:
- Finance:PenaltyBasisPoints / Finance__PenaltyBasisPoints
- Finance:DailyInterestBasisPoints / Finance__DailyInterestBasisPoints
- Finance:DailyDiscountBasisPoints / Finance__DailyDiscountBasisPoints

Defaults: 500, 200, 100 (5%,2%,1%). Valid range for each: 0..10000. Existing receivables retain their snapshots; configuration changes affect newly generated receivables.

## Executed examples
- Stock 15; requests 5/8/9; approvals 4/5/6 -> fulfilled 15, shortage 7, eligible stock 0, expired stock untouched.
- Alternative manual 5/8/2 also accepted; no customer priority is inferred.
- Sale 235.00 on 2026-10-19, code 10 -> 117.50 due 2026-10-26 and 117.50 due 2026-11-02. This used a deterministic test clock, not a production backdating endpoint.
- Code 20,100.01 ->33.33/33.33/33.35, exact total 100.01.
- P100.00: due today100.00, one day late107.00, two days late109.00, one day early99.00. Discount never makes a negative payable.

## Reproduce verification
```powershell
dotnet test VanderCake.Server.slnx --configuration Debug --logger trx --results-directory artifacts/TestResults
pwsh -NoProfile -File scripts/Verify-Persistence.ps1 -CompleteWorkflow
```
The script uses generated credentials and a disposable database, executes the real HTTPS workflow, restarts the server and verifies persisted financial state/debt. It does not reset the normal database. Localhost certificate-trust skipping is limited to this test client; no application TLS bypass exists.

See ADR-003 for SQLite concurrency and migration limitations, and TEST_LOG for actual results and NOT RUN checks.
