# Requirements and acceptance criteria

Deadline: **2026-10-28**, America/Sao_Paulo. Practical total development budget: **5–10 hours**.
VanderCake is a B2B distributor of yeast, cake mixes, savory mixes and margarine.

## Scope policy
P0 is the smallest complete assessed flow: customer -> salesperson order -> manual allocation -> FEFO -> sale -> receivables -> full-installment payment. All explicitly requested business capabilities remain P0 at a basic functional level. P1 contains depth/polish, not silent removal of a required feature. Five hours is high risk; target the upper end of the budget and simple forms. Report gaps rather than claiming incomplete features are done.

Phases 01/02 delivered architecture, domain/schema, Identity and essential APIs. Phase 03 delivered the backend workflow; Phase 04 the administrator workspace; Phase 05 implements native salesperson pages and verifies the Windows flow. Android interactive verification remains pending; explicit acceptance/auxiliary gaps are in BACKLOG.md.

## Requirements

| ID / priority | Capability | Acceptance criteria | Test IDs |
|---|---|---|---|
| FND-01 / P0 | .NET 10 structure | Eight projects with documented references; server-only build works; Mobile has no reference to server/domain/persistence | FND-04, FND-05 |
| FND-02 / P0 | API + Razor host | GET /health returns 200 JSON healthy/VanderCake; / returns Razor HTML in same host | FND-01, FND-02, FND-09 |
| FND-03 / P0 | Server SQLite | Server-only persistent schema applies through migrations; integer money/FKs/constraints enforced; normal restart preserves records | FND-03, FND-05, DB01–DB05 |
| FND-04 / P0 | Native mobile | Windows/Android targets, XAML view and injected view model; attempt both builds and record any blockers | FND-06, FND-07, FND-08 |
| SEC-01 / P0 | Two-role login | Admin website and salesperson app login; invalid credentials fail; anonymous/wrong-role business requests denied; no public signup/secrets in repo | SEC-01, SEC-02 |
| CRM-01 / P0 | Portfolio/registration | Only assigned customers visible, sorted by visit sequence; new customer assigned to caller; cross-owner requests rejected; admin can reassign | CRM-01, SEC-03 |
| CRM-02 / P0 | History/debt | Confirmed purchases, fulfilled quantities and historical prices; open principal/overdue/as-of amounts; settled debt excluded | CRM-02, FIN-04, SEC-03 |
| CAT-01 / P0 | Master management | Admin creates/edits/deactivates customers, salespeople, groups and products; required fields/price/unit validated; history survives | CAT-01, SEC-02 |
| STK-01 / P0 | Stock lots | Admin receives/manages lots, views quantity/expiry; invalid quantities/dates rejected; valid/expired stock distinguished | STK-01 |
| ORD-01 / P0 | Submit order | Owned customer, positive units, valid products, authoritative prices, terms 10/20, date and 12:00/19:00; past slot rejected; retry cannot duplicate | ORD-01, ORD-02 |
| ORD-02 / P0 | Status | Salesperson sees Submitted/Fulfilled/PartiallyFulfilled/Cut, requested/fulfilled/unfulfilled units and dispatch | ORD-03, SEC-03 |
| ALC-01 / P0 | Manual allocation | Admin chooses quantities; 5/8/9 -> 4/5/6 consumes 15, shortage 7; other legal manual allocations allowed; invalid totals rejected | ALC-01, ALC-02 |
| ALC-02 / P0 | FEFO/consistency | Valid lots by expiry then ID; expired excluded; concurrent/duplicate confirmation cannot oversell/duplicate finance; rollback is atomic | STK-02, ALC-03 |
| FIN-01 / P0 | Sales | Positive fulfilled units only, immutable prices, one sale/order; zero allocation creates no finance | FIN-01, ALC-03 |
| FIN-02 / P0 | Receivables | Code10: 50/50 at +7/+14; code20: 33.33/33.33/33.34 at +7/+14/+21; cent sums equal sale | FIN-02 |
| FIN-03 / P0 | Payments/rules | Configurable penalty, simple interest and early discount; correct early/due/late values; exact full settlement updates balance atomically | FIN-03, FIN-04 |
| ADM-01 / P0 | Dashboard | Pending orders, today's sales, open/overdue principal and expiring valid units agree with detail pages | ADM-01 |
| QLT-01 / P0 | Quality | Async I/O/cancellation, DI, DTOs, server validation, xUnit high-risk coverage and actual demo evidence | All; QLT-01 |

## P1
Partial payments, credits/refunds, reversals, backorders, fractional units; rich filters/export/print/charts; route maps and visit history; finance-settings UI (P0 uses validated config); advanced stock adjustments; store packaging/release signing/polished branding; offline sync and external integrations. Offline mode requires a new architecture decision.

## Definition of done
Acceptance criteria must work with named tests/manual evidence. Builds alone do not prove runtime, authorization or finance. Placeholder login and fabricated results do not satisfy P0. Phase 01 completion is separate from whole-product completion.


## Phase 04 status
Administrator web screens now implement basic customer/product/salesperson/group management, stock receipt/inspection, actual dashboard metrics, pending and finalized order details, multi-order manual cutting, allocation history, sales/installments and full payment receipts. Test mapping UI01–UI06 and WEB04 is in TEST_PLAN.md; real results are in TEST_LOG.md.

Group deactivation and correcting an unconsumed lot remain explicit auxiliary gaps; group naming and stock receipts are available. Partial payments remain unsupported P1. Native screens and Windows runtime were subsequently delivered in Phase05; Android interactive acceptance remains open. Master/order filtering currently operates over the existing capped lists (500); large-data pagination is P1.

## Phase 05 native status and remaining acceptance
All requested native pages are implemented in XAML/MVVM. MOB01–MOB08 trace their actual client/server tests in TEST_PLAN.md. Windows native runtime has demonstrated the principal salesperson flow, financial/history reads, administrator visibility and secure-session/data restoration. Android has independent build and real startup-smoke evidence only; full Android interactive acceptance and physical-device setup remain open gates. See MOBILE.md for HTTPS configuration and exact runtime limits.

## Final Phase06 acceptance
The authoritative final coverage/status matrix is REQUIREMENTS_COVERAGE.md. Current execution:116 passing tests, full Debug solution Windows/Android build, exact repeat-safe XYZ/235/payment rehearsal, real HTTPS restart and12 authenticated Razor routes. Product-group deactivation and unconsumed-lot correction remain incomplete P0 auxiliary criteria, irrespective of earlier P1 wording. Android interactive/device acceptance and human delivery rehearsal remain open. Do not infer complete P0 acceptance from build/test totals.
