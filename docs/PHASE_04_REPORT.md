# Phase 04 delivery — administrator web UI

Completed 2026-10-22, America/Sao_Paulo. No Phase 05, push or deployment.

## Screens delivered
Administrator login; actual dashboard; customer/portfolio management and balance; salesperson account management; product catalog and groups; stock-lot inspection/receipts; pending/finalized order list and details; single/batch manual allocation; allocation audit/lot movements; sales and generated installments; receivables, settlement quote and payment receipt.

Shared responsive sidebar/layout, restrained corporate palette, labels/focus/skip navigation, tables/cards/status badges, filters, empty states, form errors, success feedback and submit busy state. Razor output encoding and antiforgery remain enabled. No invented dashboard data.

## Implementation and files
- New application files: `Abstractions/Administration.cs`, `Services/AdministrationService.cs`. New Infrastructure adapter: `Persistence/AdministrationStore.cs`; registered in `DependencyInjection.cs`.
- Domain methods added for salesperson name/status updates and group rename; no entity/schema/package changes.
- New Server page infrastructure: `Pages/Admin/AdminPageModel.cs` and `Index.cshtml.cs`.
- New Razor page/model pairs in `Pages/Admin/`: `Customers`, `Salespersons`, `Products`, `Groups`, `Stock`, `Allocations`, `Order`, `Sales`, `Sale`.
- Updated `Index`, `Orders`, `Receivables`, `Allocation`, `AllocationHistory`, `Payment` views/affected models, Account login, shared layout and public landing page.
- New `wwwroot/js/site.js`; replaced `wwwroot/css/site.css`. Request culture explicitly set in `Program.cs` to keep English form decimal binding consistent on this Portuguese Windows host.
- New `tests/VanderCake.IntegrationTests/AdministratorUiTests.cs` (six scenarios; real SQLite/Identity/Razor forms).
- New disposable review helpers: `scripts/Start-AdminReview.ps1`, `scripts/Stop-AdminReview.ps1`.
- Updated README and docs REQUIREMENTS, ARCHITECTURE, BUSINESS_RULES, BACKLOG, TEST_PLAN, TEST_LOG, DEMO; this report is new.

All page business writes call application services. Existing atomic FEFO/sale/receivable/payment services are reused. New Identity/profile updates are transactional, use existing password hashing and invalidate sessions through security stamps. No password is shipped in source. No DbContext queries or finance calculations were added to page handlers.

## Actual verification
- Final `dotnet build VanderCake.Server.slnx --no-restore -v minimal`: PASS, 0 warnings/errors, 4.06 seconds.
- Final `dotnet test VanderCake.Server.slnx --no-restore --no-build --logger 'trx;LogFilePrefix=phase04-release' --results-directory artifacts/verification`: PASS, **104/104** (38 unit +66 integration), 0 failed/skipped.
- Six added tests cover all main routes/cookie challenges, 4/5/6 batch form and repeat confirmation, financial follow-through, customer/product/group changes and safe encoding, receipt validation, salesperson provisioning/deactivation and stale/antiforgery rejection.
- Actual HTTPS browser review performed stock/order inspection, live over-stock rejection, requested22/approved15/shortage7, three sales187.50/six installments, payment29.06 on Bakery B principal31.25, updated customer debt and global open principal156.25, Paid filter and dashboard. Normal restart preserved state and cookie.
- Responsive layout inspected at wide/narrow settings; table overflow remained contained. Console warning/error inspection returned none. Viewport overrides were reset.
- Real captures are ignored under `artifacts/verification/`. `admin-dashboard-viewport.jpg` is an unmodified normal-viewport capture of the final dashboard. Some full-page exports at overridden sizes were cropped by the browser tool; they are not claimed as complete full-width evidence.
- The successful disposable review server/database and generated credential metadata were removed; screenshots/logs retained. Normal server database was not reset.

TEST_LOG.md retains the failed sandbox build, intermediate failing test runs and corrections, initial review setup failure, exact command prefixes/results and NOT RUN limitations. Passing final results do not erase those failures.

## Remaining work and limits
Native authentication/business screens and actual Windows/Android runtime proof remain pending. No mobile source/shared Contracts changed, so no new native build claim is made. Auxiliary gaps: unconsumed-lot corrections, group deactivation, email/password reset and extended reference CRUD; partial payments remain P1. Lists retain existing500-row caps; batch maximum100, one explicitly selected common date/window. Formal accessibility certification, keyboard-only full rehearsal, additional browsers/load/power-loss/migration interruption are NOT RUN.

No new backend dependency is missing. Before a later client phase, read AGENTS.md and the updated backlog; preserve tested service/security/contracts boundaries, use private human-selected credentials and a trusted development HTTPS configuration, then implement and verify the native P0 flow. Begin only when requested.
