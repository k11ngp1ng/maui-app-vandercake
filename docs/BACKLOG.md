# Final backlog — Phase06, 2026-10-22

Deadline2026-10-28. Preserve delivery stability; no new modules. DONE means the named implementation/check is complete, not every platform is accepted.

| Priority | Status | Work / acceptance |
|---|---|---|
| P0 | DONE | Domain/invariants, server SQLite migrations/persistence, Identity roles/ownership, structured API errors |
| P0 | DONE | Customer portfolio/registration/visit order, products/orders/dispatch, historical purchases/debt |
| P0 | DONE | Explicit manual batch allocation, FEFO/expiry, shortages, atomic sale/installments/payment, duplicate/concurrent/rollback protection |
| P0 | DONE | Razor login/dashboard, customer/salesperson/product/group naming, stock receipt/inspection, order/allocation audit, sales/receivables/full payment |
| P0 | DONE | All seven native salesperson feature areas with XAML/MVVM, HTTPS, SecureStorage, cancellation/error states |
| P0 | DONE | Windows native login/order/status/history/debt and session restoration (Phase05 runtime evidence) |
| P0 | DONE | Final backend tests116/116; full Debug solution Windows/Android; applied migrations/no drift; HTTPS restart proof |
| P0 | DONE | Exact XYZ fixture5/8/9 ->4/5/6, FEFO6+9,235.00 example, settlement/debt; repeat-safe seeding and script retries |
| P0 | DONE | Final documentation, requirements coverage, launch/runbook and evidence index |
| P0 auxiliary | TODO | Product-group deactivation (currently create/rename only); preserve history and validate dependent products before claiming full CAT-01 |
| P0 auxiliary | TODO | Correct an unconsumed lot (receipt/inspection available); must preserve consumed history before claiming full STK-01/BR-15 |
| P0 verification | TODO | Android interactive HTTPS login/portfolio/detail/registration/order/status and touch layout; build/startup alone do not close this |
| Delivery | TODO | Human-chosen credentials, trusted endpoint, dedicated persistent demo DB and combined native/web presentation rehearsal on delivery machine |
| Quality | TODO | Formal accessibility/keyboard audit, physical Android connectivity and large-portfolio checks; NOT RUN |
| P1 | TODO | Partial payments, credit/refund/reversal, backorders/fractional units |
| P1 | TODO | Pagination beyond500, expanded reference CRUD, email/password reset, advanced stock adjustments |
| P1 | TODO | Exports/charts/maps/visit history, finance-settings UI (validated config already exists) |
| P1 | TODO | Standards-based production identity/HTTPS/key operations, Release packaging/signing/distribution |
| P1 | TODO | Offline sync/integrations (requires new architectural decision) |

No work remains IN PROGRESS after this QA turn. No installed SDK/build prerequisite is BLOCKED. Unexecuted runtime acceptance is TODO/NOT RUN, not a fabricated environment blocker or PASS.

Known risks: single-writer/single-server SQLite; interrupted migration/load/power-loss not tested; nontransactional PRAGMA during table rebuild;500-row list caps; in-memory native drafts; development passwords preserved on reseed (no reset); aging demo lots/dispatch requests need a new explicitly selected fixture, never stock replenishment/reset.

Delivery decision: **conditionally ready for a Windows + web academic demonstration; not unconditional completion of all P0/platform criteria**. Close the TODO delivery/runtime gates and disclose auxiliary gaps to the assessor. Do not automatically begin another phase.
