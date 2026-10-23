# Final requirements coverage — 2026-10-22

Implementation and execution are separate. PASS below means the named check ran; prior visual/native evidence is dated by phase in TEST_LOG.116 executed tests =38 unit +78 integration; no failures/skips. No unavailable platform is treated as accepted.

| Requirement | Implementation / acceptance | Traceable evidence | Final gap |
|---|---|---|---|
| FND-01 | DONE: eight-project .NET10 architecture and proper references | Full solution build; FND-04/05; architecture | None for Debug build |
| FND-02 | DONE: same-host health/API/Razor | FoundationTests FND-01/02;12 real authenticated routes Phase06 | Health is liveness, not database readiness |
| FND-03 | DONE: EF SQLite/money/constraints/persistence | PersistenceTests DB01–05; MigrationUpgradeTests; Phase06 migrations/no drift and real restart | Power loss/interrupted migration NOT RUN |
| FND-04 | DONE implementation/build; PARTIAL platform runtime acceptance | Full Debug Windows/Android build; NATIVE05-WIN; NATIVE05-ANDROID startup | Android interactive flow NOT RUN |
| SEC-01 | DONE academic login/auth schemes | ApiTests/AdminCookieTests/MOB03–06; Phase06 actual cookie login | Production identity/HTTPS/key operations deferred |
| CRM-01 | DONE portfolio/visit sequence/registration/reassignment | ApiTests CRM01/SEC03; UI03; MOB01/02; prior Windows portfolio | Native registration UI manual acceptance NOT RUN |
| CRM-02 | DONE fulfilled history/debt | WorkflowTests ALC01/FIN04/SEC03; MOB01/02; prior Windows reads; Phase06 balances | None in server semantics |
| CAT-01 | PARTIAL: customer/salesperson/product management and group create/rename | ApiTests; AdministratorUiTests UI03/05/06 | Group deactivation absent (P0 auxiliary); advanced account changes P1 |
| STK-01 | PARTIAL: receipt/inspection/dates/quantity | DomainTests; UI04; Phase06 XYZ stock0 | Unconsumed-lot correction BR-15 absent (P0 auxiliary) |
| ORD-01 | DONE submission, authoritative snapshots, terms/windows/validation/retry | ApiTests ORD01/02; MOB01/07; native Windows order; Phase06 requests | Memory-only draft after native process death |
| ORD-02 | DONE statuses/shortages/finalization protection | WorkflowTests/EdgeTests; MOB01; native Windows refresh | No physical-delivery tracking (explicit assumption) |
| ALC-01 | DONE explicit manual quantities, over-request/stock validation | WorkflowTests ALC01/02; UI02; prior WEB04 manual UI; Phase06 exact XYZ | Script is explicit rehearsal, not auto priority |
| ALC-02 | DONE FEFO/expiry/idempotency/concurrency/atomic rollback/audit | WorkflowTests STK02/ALC03; WorkflowEdgeTests; Phase06 FEFO6+9/same IDs | Single-server SQLite limits |
| FIN-01 | DONE sale only positive fulfilled quantities/snapshot prices | WorkflowTests FIN01/ALC01/03; Phase06 sales150.00+235.00 | No refund/reversal |
| FIN-02 | DONE both exact schedules/cent sums/due dates | WorkflowRuleTests FIN02; WorkflowTests FIN02_235...; Phase06117.50+117.50 | Live date22Oct; deterministic19Oct tested separately |
| FIN-03 | DONE quote/rates/full settlement/payment history | Domain/WorkflowRuleTests FIN03; WorkflowTests/Edge/Configuration FIN04; Phase06 payment18.60/debt365.00 | Partial payments P1 |
| ADM-01 | DONE persisted dashboard metrics | AdministratorUiTests UI01/02; prior WEB04; Phase06/Admin200 | No invented widgets; advanced reports P1 |
| QLT-01 | PARTIAL final acceptance: DI/async/DTO/errors/tests/docs implemented |116 tests; real HTTPS/restart/routes; prior native/web evidence | Android interaction, formal accessibility/device/large-data checks open |

## Additional delivery acceptance
| ID | Exact criterion | Evidence/status |
|---|---|---|
| DEMO06-SEED | Repeat seed does not duplicate/refill/reset password | AcademicSeedTests.DEMO06_RepeatSeedPreservesCredentialsAndConsumedStockWithoutDuplicates: PASS |
| DEMO06-FLOW | One admin/seller, three customers;XYZ15,5/8/9->4/5/6,stock0,sales150.00,six installments | Real dedicated seed/API rehearsal: PASS; standard base seed creates accounts, no additional users |
| DEMO06-235 | Separate235.00 ->117.50/117.50,+7/+14 | Real sale22Oct due29Oct/5Nov: PASS; fixed19Oct scenario automated PASS |
| DEMO06-PAY | Full first XYZ installment, component history and updated debt | Principal20.00, early discount1.40,paid18.60; combined principal365.00: PASS |
| DEMO06-RETRY | Repeat completion produces same allocation/payment IDs | phase06-demo-complete/repeat.txt: PASS |
| DB06-RESTART | Normal new process retains finalized sale/AR/payment/debt/original tokens | phase06-persistence.txt: PASS |
| WEB06-ROUTES | Real HTTPS antiforgery cookie login and12 protected pages render | phase06-web-routes.txt: PASS; no new visual assertion |
| DELIVERY06 | Chosen credentials/certificates/new fixture and complete human native/web rehearsal | TODO; delivery machine setup was not performed by a human here |

Test IDs refer to TEST_PLAN and executable test names, not invented extra test counts. Details/failures and artifact filenames are in TEST_LOG. Full P0 completion cannot be asserted while CAT-01/STK-01 auxiliary criteria and Android interactive acceptance remain open.
