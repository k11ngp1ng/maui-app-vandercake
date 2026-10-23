# Business rules

Status after Phase06 QA: backend, administrator workspace and native salesperson pages are implemented. Windows native flow is verified; Android interactive acceptance remains pending beyond build/startup smoke. Unconsumed-lot corrections, group deactivation and partial payments remain deferred; see BACKLOG.md. No financial or allocation policy was changed or duplicated in the native client.
Rules marked "assumption" resolve gaps for a small achievable demo; change them explicitly with matching tests if the user supplies different rules.

## Roles, customers and products
- BR-01: Administrator manages master data, stock, allocation and payments. Salesperson only sees/uses their assigned customers, history, balances and orders. Client IDs never establish authorization.
- BR-02: A new salesperson-created customer is assigned to that salesperson by the server. Administrator can assign/reassign customers. Visit sequence is a positive integer per portfolio; sort by sequence then customer ID; duplicates are allowed and deterministically ordered (assumption). No route optimization.
- BR-03: Products belong to a group, have an active flag, positive unit price and a defined sale unit. P0 quantity is an integer number of sale units (assumption); no weight conversion. Product groups include yeast, cake mixes, savory mixes and margarine.
- BR-04: Referenced customers/users/products are deactivated, not destructively deleted. History retains price, product description and customer/salesperson snapshots where needed.

## Order and dispatch
- BR-05: Server accepts positive requested quantities, active products/customer/owner, term code 10 or 20 and a dispatch window exactly 12:00 or 19:00. Reject empty orders, duplicate product lines and invalid/negative/overflowing values.
- BR-06: Dispatch has a calendar date plus window in business time zone America/Sao_Paulo (assumption). Reject a dispatch instant at or before current business time at submission AND fulfillment. Expired slots require an administrator to select a valid future slot before confirmation; never silently ship late. Dates use calendar days, no holiday/weekend shift.
- BR-07: Server snapshots current prices at submission; client-supplied totals/prices are never authoritative. Submitted order is immutable to the salesperson. P0 has no persistent draft or automatic retry queue.
- BR-08: Submitted -> Fulfilled if all lines fulfilled; -> PartiallyFulfilled if total fulfilled > 0 but below requested; -> Cut if fulfilled total is zero. These are terminal in P0. "Fulfilled" means allocation confirmed for dispatch, not proof of physical delivery. Unfulfilled units are recorded and closed; no automatic backorder. A new order is a new request.
- BR-09: Submission key scoped to salesperson prevents network retries creating duplicate orders; repeat key with changed content is a conflict.

## Manual allocation, stock and FEFO
- BR-10: Administrator explicitly chooses each fulfilled quantity: 0 <= fulfilled <= requested. Aggregate allocations for a product cannot exceed available valid stock. A batch may cover several submitted orders; validate all and commit atomically.
- BR-11: Required example: stock 15; A requests 5/gets 4; B requests 8/gets 5; C requests 9/gets 6. Requested = 22; fulfilled = 15; unfulfilled = 7 (1 + 3 + 3). A=5/B=8/C=2 is also legal if explicitly chosen; the example defines no automatic priority.
- BR-12: Valid lot has positive remaining units, receipt date <= today's business date and expiry date >= scheduled dispatch date AND >= today's business date. Expiry date itself is valid through that calendar day (assumption). Exclude expired or unreceived lots even if a stored physical quantity remains.
- BR-13: After manual customer quantities are chosen, consume eligible lots by earliest expiry, then lot ID. FEFO chooses lots, never customer allocation. Stock views distinguish physical, valid, expired and soon-to-expire quantities; default soon-to-expire horizon 7 days is configurable (assumption).
- BR-14: Revalidate stock and versions inside the confirmation transaction. Never accept negative stock, concurrent overselling, over-fulfillment, repeated sale generation or consumption of expired lots. Stale edits return a conflict for administrator review.
- BR-15: Lot quantities are increased through explicit receipts; P0 requires correction of an unconsumed lot; this auxiliary capability remains NOT IMPLEMENTED at final QA. Never silently edit a consumed balance; advanced adjustment/reversal workflows are P1.

## Sale, money and installments
- BR-16: Only positive fulfilled quantities generate sales. Each order has at most one sale. Zero-fulfilled orders create no sale/receivables. Use the submitted price snapshot, not today's product price.
- BR-17: BRL, two decimals (assumption). Calculate using decimal; persist integer cents. Line total = fulfilled units * unit price. Sale total is the exact sum of line totals. Never use double/float.
- BR-18: Due dates are sale's business date + calendar offsets. Code 10: 50% at +7 days and 50% at +14 days. Code 20: 33.33% at +7, 33.33% at +14, 33.34% at +21.
- BR-19: Round each non-final installment to cents using MidpointRounding.AwayFromZero; final installment is total minus prior installments. Cap earlier allocations at remaining cents; omit zero-value installments. Sum must equal sale exactly, including tiny totals. Examples: 100.00/code20 -> 33.33 + 33.33 + 33.34; 100.01/code10 -> 50.01 + 50.00.
- BR-20: Snapshot term offsets/percentages and configured settlement rates at sale creation. Editing configuration cannot retroactively change existing debts.

## Payments and customer debt
- BR-21: Configurable demo rules: one-time late penalty 5%, daily simple interest 2%, daily early-payment discount 1%. Validate nonnegative rates and penalty/discount rates <= 100%; constrain daily interest configuration to <= 100% for the demo. Configuration file/options are P0; an administrator settings UI is P1. These are project examples, not a claim about commercial policy.
- BR-22 (P0 assumption): Register settlement of a whole open installment. Partial settlement, credits, refunds and reversals are P1. Persist the actual amount, date and component breakdown; do not merely toggle a paid flag.
- BR-23: For unpaid principal P and calendar payment date D, compare with due date:
  - On due date: amount = P.
  - L days late: penalty = round(P * 0.05); interest = round(P * 0.02 * L); amount = P + penalty + interest. Interest is simple, never compounded.
  - E days early: discount = min(P, round(P * 0.01 * E)); amount = P - discount, never negative.
  - Round each component to cents AwayFromZero; no early discount and late charge together. Use stored rates rather than literals in implementation.
- BR-24: Examples on P=100.00: two days late -> 109.00; three days early -> 97.00; due date -> 100.00. Excessive early days cap discount at 100.00. Zero-due settlement is permitted with explicit administrator confirmation.
- BR-25: P0 payment date is server business date; no future/backdated entries (assumption). Requote and validate exact payment amount on save. Reject under/over-payment, already-settled installments and conflicting retries; idempotent repeat request returns existing payment. Atomic payment and settlement state update.
- BR-26: Customer debt shows open original principal plus separately calculated as-of-date settlement amounts and overdue principal; settled installments are excluded. No penalties materialize daily in a background job. Sales history uses confirmed sales/fulfilled quantities and snapshot prices, not merely submitted orders.
- BR-27: Dashboard P0: submitted-order count, sales total today, open principal, overdue principal and expiring valid units in the next 7 days. Use the same business clock/date definitions as detail pages.
- BR-28: Allocation batch has a globally unique request key, canonical content fingerprint, administrator and UTC time. Matching retries return the same outcome; changed content or re-finalizing an order under another key conflicts. Even Cut orders retain an auditable batch; only positive quantities generate movements/sales.
- BR-29: Installment state is Open through its due date, Overdue after it while unpaid, and Paid after settlement. Persist Open/Paid and derive Overdue at read time. No scheduled state/interest job runs.
- BR-30: A payment request carries expected quote date/version/amount for confirmation. The server always uses today's business date and its own calculation. A stale quote conflicts; a mismatched amount is rejected. Matching payment-key retries return saved history without another charge, including after the original payment date.

## Decisions to validate before the affected module
Technical demo bounds added in Phase 02: 1–1,000,000 sale units per line/lot, at most 100 distinct order lines and at most 1,000,000,000,000 cents per price/order/sale total. Monetary input rounding is AwayFromZero; request decimal-limit validation is invariant across locales. Payment terms 10/20 are immutable seeded reference definitions in this phase.

Order access after reassignment requires original salesperson authorship AND current customer assignment. The former salesperson loses access; the new owner does not inherit the old order as their submitted order. Implemented purchase history/debt queries use current customer ownership.

Currency BRL, integer units, expiry inclusive, closed shortages, full-installment-only settlement, calendar day treatment, and the visit-sequence tie rule are explicit working assumptions. They are not claims inferred from the allocation example. An unresolved change must be reflected in requirements/tests before implementing it.


