# Test plan and traceability

Current Phase06:38 unit and78 integration tests pass (116 total). Historical Phase03 baseline was38+60. The foundation table retains its original checks; phase-specific coverage below distinguishes executable backend acceptance from remaining native/visual/P1 work. No dummy passing tests are included.

## Foundation

| ID | Requirements | Check | Status |
|---|---|---|---|
| FND-01 | FND-02 | WebApplicationFactory /health: 200 JSON healthy/VanderCake | Automated pass |
| FND-02 | FND-02 | Same host /: 200 HTML containing VanderCake | Automated pass |
| FND-03 | FND-03 | Resolve SQLite provider, open in-memory connection, SELECT 1 | Automated pass |
| FND-04 | FND-01 | Server solution build | Pass |
| FND-05 | FND-01/03 | Project graph, Mobile only Contracts, no tracked DB | Pass |
| FND-06 | FND-04 | Windows Debug compile | Pass; not runtime |
| FND-07 | FND-04 | Android Debug compile | Pass; not device |
| FND-08 | FND-04 | Launch native apps without startup crash | Windows UI and Android startup smoke performed Phase05; Android interaction NOT RUN |
| FND-09 | FND-02 | Real Kestrel /health and Razor /, stop own process | See TEST_LOG |

## Phase 02 executed coverage (historical baseline)
- SEC-01/02/03: valid/invalid login, lockout, opaque-token tamper/expiry/logout/disabled-user/stale-role rejection; scheme isolation; admin cookie security/antiforgery/local redirects; cross-owner customer/order access and forged owner assignment; reassignment revocation; HTTPS enforcement including login form delivery.
- CRM-01/CAT-01/ORD-01/02: portfolio order, customer create/update/stale version, product create/price rounding/uniqueness, authoritative order prices, idempotency, dispatch/term/quantity/duplicate-line validation and inactive customers. Remaining management UI/master CRUD is pending.
- Domain: quantity/date/stock/price invariants, explicit fulfillment/financial state transitions, sale snapshot rules, installment rounding/calendar boundaries, simple-interest/discount calculations and culture-independent DTO validation.
- DB01–DB05: applied migration/no model changes/reference seed, real SQLite CHECK/FK enforcement, two-context concurrency, separate-host persistence, sale/receivable/payment graph and SQLite integer money storage.
- Real-process verification: explicit migration + Development seed, HTTPS login/order creation, normal process restart with same order and original token; Production seeding refused. These are script checks in TEST_LOG, not additional xUnit counts.

Phase 03 now covers the operational allocation/finance/history/debt backend as follows; dashboard/native screens remain pending.

## Phase 03 required acceptance cases — executed

| Requirement/case | Executed evidence | Result |
|---|---|---|
| ALC-01 manual partial allocation | Required5/8/9 ->4/5/6: each customer's exact approved quantity, stock 15 consumed, shortage 7, sales/debt only fulfilled units | PASS |
| ALC-01 no automatic priority | Alternative 5/8/2 split is also accepted | PASS |
| ALC-02 over-stock / over-request / negative / malformed lines | Reject individual and aggregate batch overflow, duplicate/missing/foreign lines without effects | PASS |
| STK-02 FEFO/expiry | Earliest valid expiry, ID tie, expired-only rejection, inclusive expiry and future dispatch/receipt eligibility | PASS |
| ALC-03 duplicate/finalized/stale | Same-key retry, changed content conflict, new-key repeat conflict, concurrent duplicate, stale version and elapsed/rescheduled window | PASS |
| ALC-03 concurrency | Concurrent allocation requests cannot oversell; concurrent payment requests settle only once | PASS |
| ALC-03 allocation rollback | Inject finance failure after written inventory/order state; fresh context verifies no batch/movements/sales/receivables or stock/status effect | PASS |
| SEC-02/03 | Only admin mutates; other portfolios cannot read balance/purchases/sales/installments/receipts; administrator form antiforgery/session path | PASS |
| FIN-01/02 sale/terms | Sale from fulfilled units; zero Cut produces no finance;235.00 on 2026-10-19 -> 117.50 due 2026-10-26 and 117.50 due 2026-11-02; code 20 cent remainder/calendar dates | PASS |
| FIN-03 rates/boundaries | Due/early/late, one-time penalty, simple interest, half-cent rounding, discount cap, zero rates/config validation and stored snapshots across new config | PASS |
| FIN-04 debt/payment | Open/Overdue/Paid, original vs overdue vs payable amounts, successful history, exact-amount/date/version validation, idempotency, zero-value confirmation | PASS |
| FIN-04 payment rollback | Inject receipt failure after written Paid state; fresh context sees original Open version and no receipt | PASS |
| DB-06 schema upgrade | Apply Phase 02 schema, insert legacy order/items, upgrade, verify records/stock preserved | PASS |
| QLT real HTTPS persistence | Script runs complete workflow and normal Kestrel restart; financial state/debt/tokens preserved | PASS |
| Partial payments | P1 unsupported; no partial-settlement implementation | NOT RUN |
| Native runtime / screenshots / visual browser review | Shared-contract builds only; no native launch or visual evidence | NOT RUN |
| Multiple servers / sustained load / power loss / interrupted migration | Beyond the tested academic single-server scope | NOT RUN |

Razor integration tests render real forms and post antiforgery tokens; they do not constitute screenshot/visual inspection. Exact commands/counts/failures are in TEST_LOG.md.

## P0 target cases (partly covered above; remaining work is planned)

| ID | Requirements | Layer | Scenarios / expected result |
|---|---|---|---|
| SEC-01 | SEC-01 | Integration | Valid/invalid logins, lockout, no public signup, anonymous business request denied |
| SEC-02 | SEC-01, CAT-01 | Integration | Salesperson denied admin stock/masters/allocation/payments; missing antiforgery rejected; native scheme explicit |
| SEC-03 | CRM-01/02, ORD-02 | Integration | Other portfolio IDs cannot expose customer/order/history/debt; spoofed salesperson ID not trusted |
| CRM-01 | CRM-01 | Unit/integration | New customer owner=caller; admin reassignment; sequence/ties; required fields |
| CRM-02 | CRM-02 | Integration | History uses fulfilled units/snapshot price after catalogue edit; debt matches installments |
| CAT-01 | CAT-01 | Integration/manual | Every required master can create/edit/deactivate; invalid fields rejected; history survives |
| STK-01 | STK-01 | Unit/integration | Positive receipt; invalid quantity; expiry/valid/expired counts; adjustment restrictions |
| STK-02 | ALC-02 | Unit/integration | FEFO multi-lot; expired excluded; expiry on dispatch eligible; future dispatch excludes too-early expiry; ID tie |
| ORD-01 | ORD-01 | Integration | Own customer; terms10/20 and both windows; reject empty/duplicate lines, invalid code/time, past slots, inactive masters |
| ORD-02 | ORD-01 | Integration | Authoritative price snapshot; duplicate key idempotent; same key/different payload conflicts |
| ORD-03 | ORD-02 | Unit/integration | Full/partial/zero statuses and shortages; terminal order cannot reconfirm; no implicit backorder |
| ALC-01 | ALC-01 | Unit/integration | Stock 15, requests 5/8/9, manual4/5/6 -> stock 0, shortage 7; legal5/8/2 also accepted |
| ALC-02 | ALC-01 | Unit/integration | Negative, over-request, over-stock, wrong-product and stale-dispatch allocations rejected; no auto priority |
| ALC-03 | ALC-02, FIN-01 | SQLite integration | Competing confirmations cannot oversell; repeat creates one sale; stale version conflicts; injected failure rolls back all writes |
| FIN-01 | FIN-01 | Unit/integration | Fulfilled units * snapshot cents; zero cut -> no finance; unique sale/order |
| FIN-02 | FIN-02 | Unit | Percentages/dates; 100/code 20=33.33/33.33/33.34; 100.01/code 10=50.01/50.00; tiny cents, leap/month/year dates, exact sums |
| FIN-03 | FIN-03 | Unit | P100: due100, 2 days late109, 3 days early97; discount cap; simple interest; rate validation and immutable snapshot |
| FIN-04 | FIN-03, CRM-02 | SQLite integration | Full payment clears debt; mismatch/backdate/duplicate conflict rejected; repeat key idempotent; atomic balance/payment; principal vs quote |
| ADM-01 | ADM-01 | Integration/manual | Pending/today sales/open/overdue/expiring counts match details and business clock |
| QLT-01 | QLT-01 | Manual/integration | Loading/error/retry UX, cancellation, no secrets/DB in source, both native end-to-end paths, safe HTTP errors |

Use deterministic TimeProvider and table-driven arithmetic/date tests. Use real SQLite (open in-memory connection or isolated temporary file) for FK/transactions/query/concurrency; EF InMemory cannot prove these. Concurrency tests use independent DbContexts and verify resulting data. Authorization tests try other owners' IDs. Idempotency tests cover both repeated and changed payloads.

Manual evidence names OS/device, actions, observed result and actual screenshots if captured. Never claim UI proof from a build. Record failed/blocked checks.

## Commands
Run from repository root:
- dotnet build VanderCake.Server.slnx --configuration Debug --nologo
- dotnet test VanderCake.Server.slnx --configuration Debug --no-build --nologo --logger trx --results-directory artifacts/TestResults
- dotnet build src/VanderCake.Mobile/VanderCake.Mobile.csproj -f net10.0-windows10.0.19041.0 --configuration Debug --nologo
- dotnet build src/VanderCake.Mobile/VanderCake.Mobile.csproj -f net10.0-android --configuration Debug --nologo
- pwsh -File scripts/Test-Health.ps1

Raw outputs are ignored under artifacts/. Keep durable summaries in TEST_LOG. Rerun affected tests; build both mobile targets after Mobile/shared DTO changes.


## Phase 04 UI acceptance / traceability
| Executed test | Requirements | Acceptance evidence |
|---|---|---|
| UI01 | SEC-02, CAT-01, ADM-01, QLT-01 | Cookie challenge for all 10 admin screen routes; authenticated rendering, navigation, assets and empty states |
| UI02 | ALC-01/02, FIN-01/02/03, CRM-02, ADM-01 | Real Razor batch 5/8/9 -> 4/5/6; duplicate form confirmation, stock 0 eligible, three sales, six exact installments, payment receipt, reduced balance/dashboard |
| UI03 | CAT-01, CRM-01, SEC-02 | Customer create/edit/deactivate, group create/rename, product create/edit/deactivate and exact decimal price; hostile HTML encoded |
| UI04 | STK-01, CAT-01 | Stock receipt persists; duplicate lot and expired receipt show form errors without invalid stock |
| UI05 | SEC-01/02, CAT-01 | Salesperson account creation, name/status edit, existing bearer denied after deactivation |
| UI06 | SEC-02, CAT-01, ALC-01, QLT-01 | All management/batch POST forms reject missing antiforgery; stale product update rejected; empty selection shows validation |
| WEB04 manual | ADM-01, ALC-01, FIN-01/02/03, QLT-01 | Real HTTPS browser login, stock/order inspection, live over-stock error, 4/5/6 confirmation, sale/installments, full payment, balance/filter/dashboard, persisted restart and responsive layout |

Formal screen-reader audit, keyboard-only end-to-end, additional browsers and load benchmarks: NOT RUN. Browser observation and HTML focus styles are not a formal accessibility certification. Native app runtime remains NOT RUN in this phase.

## Phase 05 traceability
| Test | Requirement | Acceptance |
|---|---|---|
| MOB01 | SEC-01, CRM-01/02, ORD-01/02, QLT-01 | Exact client/VM login, portfolio/detail/debt, order submission/status/history and native-created order visible in authenticated administrator HTML |
| MOB02 | CRM-01/02, FIN-01 | Registration assigned by server; fulfilled historical quantities/prices and balance consumed through mobile API client |
| MOB03 | SEC-01/03 | Secure-session abstraction restore, exact origin binding and administrator-account rejection |
| MOB04 | SEC-01, QLT-01 | Cancelled network operation, safe network message, bad login and token expiry |
| MOB05 (four cases) | SEC-01, QLT-01 | Reject HTTP, URI credentials, API paths and query secrets |
| MOB06 | SEC-01, FND-04 | Exact unexpired localhost certificate pin only; wrong host/protocol/pin/date denied |
| MOB07 | ORD-01, QLT-01 | Simulated committed response loss freezes the draft; retry returns one original order |
| MOB08 | QLT-01 | Automatic navigation cancellation is silent; explicit Cancel gives feedback |
| NATIVE05-WIN | FND-04, SEC-01, CRM-01/02, ORD-01/02 | Actual native login -> portfolio -> detail -> new order -> confirmation -> submitted status; refresh partial status, history/purchases/debt and secure-session restoration |
| NATIVE05-ANDROID | FND-04 | Independent Debug build plus actual standalone APK install/cold-start/live-process/resumed-activity smoke; interactive flow NOT RUN |

Platform SecureStorage is proven by Windows app restart restoration, separately from memory-vault xUnit tests. Physical-device connectivity, Android interactive flow, screen-reader/full keyboard audit, Release distribution and large-portfolio performance remain NOT RUN.

## Phase06 final execution and coverage
- DEMO06-SEED: new AcademicSeedTests exercises repeated base/academic seed, unchanged counts/passwords and no refill after consuming XYZ. PASS.
- DEMO06-FLOW/235/PAY/RETRY: actual HTTPS Prepare-AcademicDemo preparation, completion and exact retry. PASS; recorded sales/FEFO/installments/payment/debt; separate from xUnit counts.
- DB06-RESTART: Verify-Persistence -CompleteWorkflow rerun, real process restart with original tokens/financial graph. PASS.
- WEB06-ROUTES: actual antiforgery cookie login and12 authenticated Razor routes including demo audit/sale. PASS; HTTP/HTML, not new screenshots.
- Full Debug solution includes both Windows/Android targets; both compile. Applied migrations listed and model drift check passed.
- Final traceability and incomplete acceptance: REQUIREMENTS_COVERAGE.md. Earlier phase statements about pending screens/native runtime are historical, superseded by Phase05/06 evidence, not current gaps.
- Android interactive/physical devices, formal accessibility, Release distribution, load and interrupted migration remain NOT RUN. Group deactivation/unconsumed-lot correction remain absent, not passing tests.
