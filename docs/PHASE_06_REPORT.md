# Phase06 final QA and academic delivery report
Date2026-10-22; deadline2026-10-28.

## Decision
**Conditionally ready for a Windows + web academic demonstration; not complete acceptance of every P0/platform criterion.** The core business workflow is reproducible and verified. Android interactive acceptance, chosen delivery credentials/HTTPS and a combined human presentation rehearsal remain open. Group deactivation and unconsumed-lot correction are explicit auxiliary P0 gaps. Disclose these if the assessor accepts delivery with limitations; otherwise close them before28Oct. No next phase, push or deployment was started.

## Implemented
Domain/model, relationships, mappings/two migrations, persistent server SQLite, cents/rounding/invariants, Identity roles/cookie/bearer/portfolio protection; customer/product/order APIs; visit order/history/debt; explicit dispatch windows/statuses; atomic multi-order allocation/FEFO/lot audit; sales from fulfilled quantities; terms10/20 exact schedules; configurable stored rates, full settlement/history/debt.

Razor: login/dashboard/customer/salesperson/product/group naming/stock receipt/order details/multi-order cutting/allocation history/sales/installments/receivables/payment. Native: all seven requested feature areas in XAML/MVVM, cancellable shared-contract HTTPS client, secure sessions, validation/error/loading/confirmation and safe in-memory retry.

## Partial and absent
Partial: CAT-01 group management lacks deactivation; STK-01/BR-15 lacks unconsumed-lot correction. Android feature code/build/startup exist but interactive flow is unverified. Formal accessibility/device/large-data quality acceptance is unverified.

Absent P1: partial payments, credits/refunds/reversals, advanced stock adjustments, backorders/fractional units, pagination beyond500, extended reference/account changes, exports/maps/visit history, settings UI, offline/integrations and production identity/release distribution. No placeholder is presented as implemented.

## Executed verification
| Check | Result |
|---|---|
| Full Debug solution build | PASS, Windows+Android+backend;0 warnings/errors,4.75s |
| Independent Windows Debug | PASS,0 warnings/errors,2.22s |
| Independent Android Debug | PASS,0 warnings/errors,2.82s |
| Automated suite | PASS116=38unit+78integration,0 failed/skipped |
| Migrations/model | Two applied, no pending migration/model change |
| Real HTTPS restart | PASS; order/sale/AR/payment/debt/original tokens persisted |
| Exact demo + repeat | PASS; XYZ5/8/9->4/5/6,FEFO6+9,stock0,shortage7; same batch/payment IDs twice |
|235.00 / payment |117.50+117.50 at+7/+14; one18.60 settlement of20.00 principal; total open principal365.00 |
| Web | Fresh real cookie/antiforgery login and12 authenticated routes200; UI form actions covered in integration tests |
| Windows runtime | Actual Phase05 native main flow/history/debt/secure restart; no new native launch Phase06 |
| Android runtime | Actual Phase05 APK install/startup/resumed activity only; interactive/device workflow NOT RUN |
| Source/script boundaries | Scripts parse, Mobile references Contracts only, no unignored runtime material |

Failures retained: new seed test first omitted required Consume date arguments (CS7036; fixed); rehearsal helper initially wrapped REST array (fixed before order creation); EF list initially selected wrong startup (corrected to Infrastructure). No failing test was removed. See TEST_LOG for commands and artifacts.

## Exact scenario and repeat safety
Development-only academic opt-in adds XYZ10.00 with valid6+9 lots and EX235235.00 with1unit. Base seed supplies one administrator/salesperson and three customers. Existing passwords/consumed lots are preserved. New regression tests repeat seeding after consumption. Request manifests are saved before API mutations; exact script retries return the same server IDs. No automatic customer allocator/reset/background job exists.

Live sale date22Oct ->29Oct/5Nov,117.50 each. Fixed19Oct ->26Oct/2Nov scenario also passes automated tests. Initial combined principal385.00; settlement20.00 principal/1.40 discount/18.60 actual cash ->365.00 principal. Report includes all movements, installments and customer balances.

## Changes in this phase
Created:
- scripts/Prepare-AcademicDemo.ps1 — explicit authenticated preparation/completion, persistent nonsecret original requests, report and safe retry.
- tests/VanderCake.IntegrationTests/AcademicSeedTests.cs — repeat seed/password/consumed-stock regression.
- docs/REQUIREMENTS_COVERAGE.md, docs/KNOWN_LIMITATIONS.md and this report.

Updated:
- src/VanderCake.Infrastructure/Identity/DemoSeeder.cs; src/VanderCake.Server/Program.cs — opt-in Development fixture only, no schema change.
- scripts/Initialize-Development.ps1 and Start-AdminReview.ps1 — optional AcademicFixture switch.
- README; ARCHITECTURE, BUSINESS_RULES, REQUIREMENTS, BACKLOG, TEST_PLAN, TEST_LOG and DEMO — actual implementation, criteria, commands/evidence and remaining scope.

## Launch and presentation
Run from repository root in PowerShell7:
```powershell
dotnet tool restore
dotnet build VanderCake.slnx -c Debug
dotnet dev-certs https --trust
pwsh -NoProfile -File scripts/Initialize-Development.ps1 -AcademicFixture
dotnet run --project src/VanderCake.Server --launch-profile https
```
Another terminal:
```powershell
& './src/VanderCake.Mobile/bin/Debug/net10.0-windows10.0.19041.0/win-x64/VanderCake.Mobile.exe'
pwsh -NoProfile -File scripts/Prepare-AcademicDemo.ps1
# Use either manual administrator confirmation OR explicit automated rehearsal:
pwsh -NoProfile -File scripts/Prepare-AcademicDemo.ps1 -Complete
```
Use chosen credentials, administrator https://localhost:7080/Account/Login and native https://localhost:7080. For a fresh exact fixture choose a new dedicated database filename before setup/server launch as DEMO specifies. Existing credentials/data are preserved; normal restarts do not run setup.

Checklist: trusted HTTPS/chosen passwords/correct persistent DB; current dispatch/expiry; assigned portfolio; XYZ15/request22/manual15/shortage7; FEFO audit; fulfilled sales/terms;235.00 dates; full payment/component history/reduced debt; normal restart; show evidence/coverage and disclose gaps. Do not mix manual completion with scripted completion of the same prepared orders.

## Evidence and remaining risk
Durable index: TEST_LOG.md, REQUIREMENTS_COVERAGE.md, DEMO.md. Raw logs/TRX and nonsecret scenario JSON: artifacts/verification/phase06-*. Prior Windows screenshots: phase05-windows-*.jpg; prior browser screenshots remain ignored locally. Generated review process/database/private credential metadata were removed; nonsecret reports/logs retained. Logs may not accompany a source checkout; regenerate them with documented commands.

No SDK/build dependency is missing locally. A physical Android device, interactive Android HTTPS/touch acceptance and delivery-machine configuration remain unverified. Single-server SQLite,500-row caps, memory-only native drafts, migration PRAGMA/interrupted-recovery limits and production identity/key/TLS/release operations remain risks. Repository source is still untracked/no initial commit: arrange a local versioned checkpoint/backup before submission; no push occurred.
