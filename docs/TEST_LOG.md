# Test log — Phase 01

Executed 2026-10-21, America/Sao_Paulo (UTC-03). Full local logs: artifacts/verification; TRX: artifacts/TestResults. These directories are ignored; this summary is durable evidence. No screenshots or native app launch evidence exist.

## Environment and scaffolding

| Command/check actually executed | Result |
|---|---|
| Get-Location; Get-ChildItem -Force; git status --short --branch | Empty repository except .git; no commits on master |
| Ancestor AGENTS.md path checks | No ancestor instructions found |
| Get-Command dotnet,git,java,adb,msbuild -ErrorAction SilentlyContinue | dotnet/git on PATH; other tools later found by full paths |
| dotnet --info (sandbox, first attempt) | Failed with Service Control Manager Access Denied; partial SDK/runtime output |
| dotnet workload list (sandbox, first attempt) | Exit1, same Service Control Manager restriction |
| Get-CimInstance Win32_OperatingSystem (sandbox) | Failed, CIM access unavailable |
| dotnet --info; dotnet workload list; dotnet new list (outside sandbox) | Exit0; SDK10.0.401, MSBuild18.9.11, Android/MAUI Windows workloads and templates present |
| dotnet --list-sdks; dotnet --list-runtimes | SDK10.0.401, runtimes10.0.12 |
| vswhere.exe -all -products * -format json / -property installationVersion | VS2026 Community18.10.12224.181 and VS2022 BuildTools17.14.37710.0 |
| Full-path java.exe -version; adb.exe version | JDK21.0.8; ADB1.0.41/platform-tools36.0.0 |
| Full-path adb.exe devices -l | Exit0, daemon started, no attached devices |
| User-profile emulator.exe -version / -list-avds / -accel-check | Version37.2.12.0, VanderBeer_API35, WHPX installed/usable |
| Optional candidate-path inventory | One batch exited1 on absent optional locations; later inspection found JDK/SDK successfully. No missing-JDK conclusion |
| dotnet new sln -n VanderCake --format slnx | Success |
| dotnet new sln -n VanderCake.Server --format slnx | Success |
| dotnet new classlib -n VanderCake.{name} -o src/VanderCake.{name} -f net10.0 --no-restore | Success, repeated for Domain, Application, Contracts, Infrastructure |
| dotnet new webapp -n VanderCake.Server -o src/VanderCake.Server -f net10.0 --no-restore | Success |
| dotnet new maui -n VanderCake.Mobile -o src/VanderCake.Mobile --no-restore | Success; restricted afterward to Windows/Android, native MVVM screen |
| dotnet new xunit -n VanderCake.{name} -o tests/VanderCake.{name} -f net10.0 --no-restore | Success for UnitTests and IntegrationTests |
| dotnet sln VanderCake.slnx add $projects; dotnet sln VanderCake.Server.slnx add $serverProjects | Success, all8 projects / all except Mobile |
| dotnet add ... reference ... | Success for graph below |
| Documentation-generation tool call | One JavaScript syntax error before execution/writes; corrected and rerun. Not a compiler/test failure |

Project reference commands executed:
- dotnet add src/VanderCake.Application reference src/VanderCake.Domain src/VanderCake.Contracts
- dotnet add src/VanderCake.Infrastructure reference src/VanderCake.Application src/VanderCake.Domain
- dotnet add src/VanderCake.Server reference src/VanderCake.Application src/VanderCake.Contracts src/VanderCake.Infrastructure
- dotnet add src/VanderCake.Mobile reference src/VanderCake.Contracts
- dotnet add tests/VanderCake.UnitTests reference src/VanderCake.Domain src/VanderCake.Application
- dotnet add tests/VanderCake.IntegrationTests reference src/VanderCake.Server

Placeholder classes/tests, Apple platform folders and bundled web frontend libraries were removed; simple Razor CSS replaces the template UI. All removals were within the freshly created workspace scaffold.

## Builds and tests

Native commands below were captured by scripts/Invoke-LoggedCommand.ps1, with -Name specifying the evidence basename and -Command dotnet. Times are observed elapsed build times, not estimates.

| Command | Result | Evidence |
|---|---|---|
| dotnet build VanderCake.Server.slnx --configuration Debug --nologo (sandbox) | **Failed**, exit1, output says build failed, 0 warnings/0 errors, no compiler diagnostic; 3.78s. Cause not proven by output | server-build.txt |
| Same command outside sandbox | **Passed**, exit0, 0 warnings/0 errors; 13.96s including restore | server-build-retry.txt |
| dotnet build src/VanderCake.Mobile/VanderCake.Mobile.csproj -f net10.0-windows10.0.19041.0 --configuration Debug --nologo | **Passed**, exit0, 0 warnings/0 errors; 22.13s | maui-windows-build.txt |
| dotnet build src/VanderCake.Mobile/VanderCake.Mobile.csproj -f net10.0-android --configuration Debug --nologo | **Passed**, exit0, 0 warnings/0 errors; 1m27.24s | maui-android-build.txt |
| dotnet test VanderCake.Server.slnx --configuration Debug --no-build --nologo --logger trx --results-directory artifacts/TestResults | **Passed**, exit0; IntegrationTests 3 passed,0 failed,0 skipped; UnitTests 0 discovered | server-tests.txt and TRX |
| pwsh -NoProfile -File scripts/Test-Health.ps1 | **Passed**, exit0; real HTTP /health200 expected JSON, /200 expected Razor heading; own server process stopped | health-http.txt, health-server-stdout.txt, health-server-stderr.txt |
| dotnet test VanderCake.Server.slnx --configuration Debug --nologo --logger trx --results-directory artifacts/TestResults/final | **Passed**, exit0; rebuilt after Razor error-page cleanup; 3 passed,0 failed,0 skipped; empty unit suite unchanged | final-server-tests.txt and final TRX |
| PowerShell XML project-reference assertions and git ls-files --others --exclude-standard path audit | **Passed**; all six production project boundaries match; no generated database, key, log, bin/obj or artifacts path in unignored inventory | Tool output; FILES_CREATED.md |

HTTP check finished at 2026-10-21 20:14:42 local (23:14:42 UTC).
The three tests are FND01_HealthReturnsPublicJsonContract, FND02_RazorPagesRunInTheSameHost and FND03_ServerResolvesWorkingSqliteProvider. The last executes SELECT1 using actual SQLite, not EF InMemory. The empty unit suite is intentional, not additional passing coverage.

Build artifacts observed:
- Windows: src/VanderCake.Mobile/bin/Debug/net10.0-windows10.0.19041.0/win-x64/VanderCake.Mobile.exe (288256 bytes).
- Android: src/VanderCake.Mobile/bin/Debug/net10.0-android/com.vandercake.mobile-Signed.apk (15859803 bytes).
- Unsigned Android APK also generated (15749648 bytes).
Artifact sizes describe this Debug run only.

## Coverage limits
- No business/auth modules, domain tests, migrations or persistent SQLite database exist yet.
- No Windows app launch, Android APK install/device run, screenshot, release build, store signing or deployment was performed.
- Full solution is structurally present; verification built server-only solution and both native target frameworks individually.
- Native runtime remains unverified; no missing compile prerequisite identified. Emulator available in separate user SDK, but no device running/connected.
- The initial sandbox failures remain failures; successful retries do not erase them.
- No git push, package publishing, workload install or next-phase implementation occurred.

## Phase 02 — domain, persistence and authentication (2026-10-21 local)

All times below use America/Sao_Paulo; raw logs use UTC (some are dated October 22 UTC). Phase 01 results above remain historical evidence.

### Changes verified
Domain/core model, real SQLite/Identity schema and migration, cents/basis-points arithmetic, application services, essential customer/product/order/reference APIs, administrator cookie forms, opaque bearer API policies, ownership/roles/stamps, validation/ProblemDetails and explicit Development seed.

Final unique executable suite: **57 tests: 26 unit + 31 integration; zero failures/skips**. Subsequent focused reruns below repeat subsets and are not counted as additional unique tests.

| Exact command / context | Actual result | Evidence under artifacts/verification |
|---|---|---|
| dotnet build VanderCake.Server.slnx --configuration Debug --nologo | Exit 0, 0 warnings/0 errors, 28.42 seconds | phase02-initial-build.txt |
| dotnet new tool-manifest --force; dotnet tool install dotnet-ef --version 10.0.12 | Scaffold/install ordering initially overwrote the first successful tool entry; install repeated after manifest creation; final root dotnet-tools.json pins 10.0.12 | phase02-ef-install.txt, phase02-ef-install-final.txt |
| dotnet ef migrations add InitialDomainAndIdentity --project src/VanderCake.Infrastructure --startup-project src/VanderCake.Infrastructure --output-dir Persistence/Migrations --no-build | Exit 0; generated migration20261022002255_InitialDomainAndIdentity, designer and snapshot | phase02-migration-create.txt |
| dotnet test VanderCake.Server.slnx --configuration Debug --nologo --logger trx --results-directory artifacts/TestResults/phase02-initial | Exit 1. Unit24 passed; integration18 passed/3 failed. One nullable test warning | phase02-tests-initial.txt + initial TRX |
| Same test command with --results-directory artifacts/TestResults/phase02-corrected | Exit 0. Unit24 passed; integration30 passed; no failures/skips | phase02-tests-corrected.txt + corrected TRX |
| dotnet dev-certs https --check | Exit 0; valid localhost development certificate found, expires2027-05-14. Trust was not changed or verified | Tool output |
| dotnet ef database update --project src/VanderCake.Infrastructure --startup-project src/VanderCake.Infrastructure --no-build | Exit 0; migration applied to explicit default LocalAppData/VanderCake/vandercake.db outside OneDrive | phase02-migration-apply.txt |
| dotnet ef migrations has-pending-model-changes --project src/VanderCake.Infrastructure --startup-project src/VanderCake.Infrastructure --no-build | Exit 0; no changes since last migration | phase02-migration-verify.txt |
| pwsh -NoProfile -File scripts/Verify-Persistence.ps1 | Exit 0; explicit --migrate --seed-demo on disposable SQLite, real HTTPS login/order create, stop/start normal server, original token/order still valid. Temporary DB cleaned up | persistence-restart.txt, persistence-initialize.txt, persistence-server-first/second-stdout/stderr.txt |
| dotnet test VanderCake.Server.slnx --configuration Debug --nologo --logger trx --results-directory artifacts/TestResults/phase02-final | Exit 0; **26 unit +31 integration passed**, no failures/skips; includes two culture cases and changed-role rejection | phase02-tests-final.txt + final TRX |
| dotnet build src/VanderCake.Mobile/VanderCake.Mobile.csproj -f net10.0-windows10.0.19041.0 --configuration Debug --nologo | Exit 0; 0 warnings/0 errors; 33.04 seconds | phase02-maui-windows-build.txt |
| dotnet build src/VanderCake.Mobile/VanderCake.Mobile.csproj -f net10.0-android --configuration Debug --nologo | Exit 0; 0 warnings/0 errors; 45.57 seconds | phase02-maui-android-build.txt |
| ASPNETCORE_ENVIRONMENT=Production; dotnet run --project src/VanderCake.Server --no-build --no-launch-profile -- --seed-demo | Expected refusal, native exit -532462766; message "Demo seeding is allowed only in Development." No database created; wrapper verified the expected refusal and exited0 | phase02-production-seed-guard.txt |
| dotnet build VanderCake.Server.slnx --configuration Debug --no-restore --nologo | Exit 0; 0 warnings/0 errors;8.38 seconds after informational-page link/text update | phase02-build-final.txt |
| dotnet test tests/VanderCake.IntegrationTests --configuration Debug --no-build --nologo --filter FullyQualifiedName~FoundationTests | Exit 0;3 foundation tests passed after page update | phase02-foundation-final.txt |
| dotnet test tests/VanderCake.IntegrationTests --configuration Debug --nologo --filter "FullyQualifiedName~SEC01_HttpCannot\|FullyQualifiedName~AdminCookieTests\|FullyQualifiedName~FoundationTests" --logger trx --results-directory artifacts/TestResults/phase02-https-final | Exit 0;8 affected tests passed after blocking login-form GET over HTTP as well as API traffic | phase02-https-final.txt + focused TRX |

The filter table escapes vertical bars only for Markdown; the executed argument contained literal OR bars.

### Failures and corrections
- Two initial API failures were a real locale bug: decimal RangeAttribute parsed its dot-decimal limits with the pt-BR host culture and threw, producing500. Set ParseLimitsInInvariantCulture and ConvertValueInInvariantCulture; added pt-BR/en-US regression tests. Both pass.
- Third initial failure was a too-specific test expecting404 for unmapped registration; ASP.NET routing returned405. Test accepts404/405 and still proves there is no signup endpoint. No registration route was added.
- Fixed one CS8604 nullable assertion warning.
- Local tool-manifest creation finished after the first install and overwrote its entry. Reinstalled after creation; migration commands then succeeded.
- One multi-file documentation patch failed its expected-line check and made no writes; reapplied with the correct text. No code/test result was affected.
- Production seed's nonzero exit is an expected guard check, not an unresolved failure.

### Evidence scope
Tests exercised actual Identity password validation and real protected tokens/cookies, not a fake authentication scheme. They use generated in-memory passwords, real SQLite migrations/check constraints/FKs and isolated temporary files. DB04 also disposes one WebApplicationFactory and opens a separate host against the same file; the separate real-process script additionally proves normal Kestrel restart persistence.

The default server database retains the migrated schema and reference data. Automated demo users/data were created only in isolated test/process databases; initialize known human-chosen credentials with Initialize-Development.ps1 before interactive login. Its password-prompt path was inspected, not run with human-chosen credentials. No passwords, signing keys, token values or database files are committed.

No native app startup, screenshot, release build/signing, deployment, push or Phase03 work occurred. Operational allocation/FEFO/sale/payment flows, remaining management UI and native integration are pending. Model arithmetic/graph persistence tests do not constitute end-to-end workflow acceptance.

Final static checks: PowerShell parser accepted Initialize-Development.ps1 and Verify-Persistence.ps1; XML assertions confirmed all six production project-reference boundaries; git source-path audit found no unignored database, key, log, bin/obj or verification-artifact files. These are static checks, not additional xUnit tests.

## Phase 03 — complete business workflow (2026-10-21 local)

Local time convention: America/Sao_Paulo. Raw command logs are UTC (October22 UTC corresponds to October21 local for these executions). Previous phase summaries remain historical evidence.

Final unique xUnit count: **98 tests:38 unit +60 integration;0 failed,0 skipped**. Script/static checks below are additional evidence, not additional xUnit counts.

### Actual executed commands

| Exact command | Actual result | Evidence under artifacts/verification |
|---|---|---|
| dotnet build VanderCake.Server.slnx --configuration Debug --nologo | Exit0;0 compiler warnings/errors;18.28 seconds | phase03-build-initial.txt |
| dotnet ef migrations add AllocationBatchAudit --project src/VanderCake.Infrastructure --startup-project src/VanderCake.Infrastructure --output-dir Persistence/Migrations --no-build | Exit0; generated20261022004717_AllocationBatchAudit, designer and snapshot | phase03-migration-create.txt |
| dotnet test VanderCake.Server.slnx --configuration Debug --nologo --logger trx --results-directory artifacts/TestResults/phase03-initial | Exit1; unit38 passed; integration42 passed/10 failed; no skips | phase03-tests-initial.txt + initial TRX |
| Same test command with --results-directory artifacts/TestResults/phase03-corrected | Exit0; unit38 +integration58 passed;0 failed/skipped | phase03-tests-corrected.txt + corrected TRX |
| Same test command with --results-directory artifacts/TestResults/phase03-final | Exit0; **unit38 +integration60 passed;0 failed/skipped** | phase03-tests-final.txt + final TRX |
| pwsh -NoProfile -File scripts/Verify-Persistence.ps1 -CompleteWorkflow | Exit0; complete real HTTPS workflow, normal Kestrel restart, persisted financial history/debt and original tokens verified; temporary DB cleaned up | workflow-restart.txt, workflow-initialize.txt, workflow-server-first/second-stdout/stderr.txt |
| dotnet ef database update --project src/VanderCake.Infrastructure --startup-project src/VanderCake.Infrastructure --no-build | Exit0; audit migration applied to existing LocalAppData/VanderCake/vandercake.db outside OneDrive; SQLite rebuild warning described below | phase03-migration-apply.txt |
| dotnet ef migrations has-pending-model-changes --project src/VanderCake.Infrastructure --startup-project src/VanderCake.Infrastructure --no-build | Exit0; no changes since last migration | phase03-migration-verify.txt |
| dotnet ef migrations list --project src/VanderCake.Infrastructure --startup-project src/VanderCake.Infrastructure --no-build | Exit0; InitialDomainAndIdentity and AllocationBatchAudit both applied, no pending migration marker | phase03-migration-list.txt |
| dotnet build src/VanderCake.Mobile/VanderCake.Mobile.csproj -f net10.0-windows10.0.19041.0 --configuration Debug --nologo | Exit0;0 compiler warnings/errors;28.39 seconds | phase03-maui-windows-build.txt |
| dotnet build src/VanderCake.Mobile/VanderCake.Mobile.csproj -f net10.0-android --configuration Debug --nologo | Exit0;0 compiler warnings/errors;24.92 seconds | phase03-maui-android-build.txt |
| dotnet build VanderCake.Server.slnx --configuration Debug --no-restore --nologo | Exit0;0 compiler warnings/errors;3.79 seconds after final footer branding cleanup | phase03-build-final.txt |

The full final suite completed at2026-10-21 around21:56 local. The real-process proof finished21:57:14 local (00:57:14 UTC October22). The footer-only change was compiled afterward and did not change business behavior.

### Required cases and observed outcomes
- Manual5/8/9 requests ->4/5/6 fulfilled: each approved amount preserved; total15, shortages7; valid inventory0; expired lot untouched; fulfilled-only sales total18750 cents and equal installment principal.
- Alternate5/8/2 accepted, proving no inferred customer priority.
- Over-request/negative/individual and combined over-stock/duplicate or foreign line input rejected with no effects.
- FEFO selected earliest eligible expiry then ID; expired/unreceived/future-dispatch-ineligible stock excluded; expiration day inclusive.
- Identical confirmation returned the same batch; changed content/new-key repeat conflicted; concurrent duplicate caused one outcome. Concurrent competing batches could not oversell.
- Zero-fulfilled order became Cut, retained audit metadata and produced no sale/debt.
- Finance fault was injected after a real SQL flush of finalized order/stock state; fresh context verified rollback of stock, order, movements, batch, sales and installments.
- Payment-history fault was injected after a real SQL flush of Paid state; fresh context verified original Open status/version and no receipt.
- Sale235.00 dated2026-10-19 under code10 produced117.50 due2026-10-26 and117.50 due2026-11-02. This used a deterministic test clock, not production backdating.
- Code20 exact remainder/calendar dates, state/date/rounding/rate boundaries, simple interest, one-time penalty, capped discount, zero rates and invalid configuration were executed.
- Debt separated open principal, overdue principal and actual current payable. Full payment retained components/history and cleared outstanding principal for that installment.
- Under/overpayment, stale version/date and duplicate payment conflict paths executed. Concurrent payment requests settled only once. Explicit zero-payable confirmation worked without negative balances.
- Role/portfolio checks protected mutations and customer/sale/debt/installment/receipt reads. Real Razor cookie/antiforgery forms completed allocation and payment.
- Upgrade from only the Phase02 migration, containing a legacy submitted order/item/stock, preserved the data after applying the audit migration.
- Real HTTPS process proof requested2/fulfilled1, generated the fulfilled sale/installments, settled one installment and preserved finalized state/history/remaining debt and both original tokens across normal restart.

### Failures and warnings
Initial integration10 failures shared one real query issue: EF could not translate filters/orderings that dereferenced a constructed ReceivableData record before materialization. Moved all filters/orderings before the final projection. Corrected and final suites passed; no client-side evaluation fallback was introduced.

The SQLite audit migration emitted: the PRAGMA foreign_keys=0 operation cannot execute inside a transaction and interruption can leave migration partially applied. Migration application succeeded and legacy-data preservation passed. This warning concerns schema maintenance, separate from the passing runtime transaction rollback tests. Stop the app and use a consistent backup for future upgrades; interrupted migration recovery is NOT RUN.

One documentation-formatting PowerShell command failed to parse because hash-literal keys are case-insensitive; no files were changed. Retried with an ordered list of replacement pairs. No build/test was affected.

### NOT RUN / unsupported
- Partial payments: NOT RUN (unsupported P1); full settlement only.
- Native app runtime/device deployment: NOT RUN. Platform builds are shared-contract compatibility evidence only.
- Browser visual inspection/screenshots: NOT RUN. Razor rendering/form posts were integration-tested.
- Multiple servers, sustained-load benchmarks, forced power loss or interrupted migration recovery: NOT RUN, outside this academic single-server verification.
- Human-chosen password setup: NOT RUN interactively in this phase; generated verification credentials never printed/committed.

No scheduled jobs, native business UI integration, next-phase work, git push or deployment occurred. Default server data was not reset; process-test data was disposable and cleaned up.

Final Phase 03 static verification: workflow-script syntax, all six production project-reference boundaries and source exclusions passed. Parsed final TRX counters confirm 38/38 unit and 60/60 integration tests passed with zero failures/not-executed entries. No database, key, log or build output appeared in the unignored source inventory.


## Phase 04 — administrator web interface — 2026-10-22 (America/Sao_Paulo)

### Actual execution
| Command / check | Result |
|---|---|
| `dotnet build VanderCake.Server.slnx --no-restore -v minimal` in restricted sandbox | FAILED exit1 with0 warnings/0 compiler errors; SDK subprocess access limitation. Retried with normal SDK access. |
| Same backend build with normal SDK access | PASS initially12.73s; final4.06s,0 warnings/0 errors. Final raw log: artifacts/verification/phase04-build.txt. |
| `dotnet test VanderCake.Server.slnx --no-restore --no-build --logger 'trx;LogFilePrefix=phase04-baseline' --results-directory artifacts/verification` | Initial38 unit PASS; integration59 PASS/1 FAIL. Failed anonymous-login cookie assertion revealed layout reading TempData on rejected login. Fixed layout to read success TempData only when authenticated. |
| `dotnet test VanderCake.Server.slnx --no-restore --logger 'trx;LogFilePrefix=phase04' --results-directory artifacts/verification` | Initial38 unit PASS, integration60 PASS/6 FAIL; one xUnit2031 warning. Product decimal Range attribute failed under host locale; display assertion assumed culture; deactivation expectation was401 but existing auth policy returns403; empty-state validation was hidden. Corrected application culture/attribute/layout; corrected expectation to actual authorization semantics and analyzer usage. |
| Same test command with prefix phase04-fixed |38 unit PASS;65 integration PASS/1 FAIL (empty allocation selection message hidden when no pending orders). Moved validation summary outside conditional empty state. |
| Same test command with prefix phase04-final | PASS38 unit +66 integration;0 failed/skipped. |
| Final `dotnet test VanderCake.Server.slnx --no-restore --no-build --logger 'trx;LogFilePrefix=phase04-release' --results-directory artifacts/verification` | PASS104 total:38 unit (307ms),66 integration (39s);0 failed/skipped. Raw log: artifacts/verification/phase04-tests.txt; release TRX timestamps20261022081946 and20261022082026. |
| `pwsh -NoProfile -File scripts/Start-AdminReview.ps1` first attempt | FAILED during API order setup:400. PowerShell array wrapping produced an invalid customer ID shape. Review process26104 and children were explicitly stopped; corrected array assignment and added failure cleanup. |
| Same review command after correction | PASS. Explicit migration/seed against a new temporary database, real HTTPS server7194 and three submitted orders. No default database changed. |
| Stop verified review process, rebuild and restart against same disposable database | PASS. No migration/seed/reset on restart; existing cookie and finalized financial state remained available. |

Initial read-only PowerShell brace-expansion command failed to parse; a large patch was rejected because it both deleted and added the same path. Neither changed files; corrected commands succeeded. Sandbox CIM process inspection reported resource access unavailable; normal-access inspection succeeded and confirmed the exact review process before cleanup. These were tooling failures, not passing application evidence.

### New integration evidence
UI01: all ten main administrator routes challenge anonymous users and render for a real administrator cookie; assets and empty states return correctly.
UI02: actual antiforgery Razor batch5/8/9 ->4/5/6, repeat form idempotency, valid stock0, three sales, six installments totaling18750 cents, linked details, full payment and reduced debt/dashboard.
UI03: customer create/edit/deactivate, group create/rename, product create/edit/deactivate;5.25 BRL persists525 cents; hostile HTML safely encoded.
UI04: valid stock receipt persists12 units; duplicate lot and expired receipt reject without adding invalid stock.
UI05: account provisioned through Identity, salesperson renamed/deactivated; existing bearer gets403 (current-user authorization rejects disabled account).
UI06: management/batch POSTs without antiforgery return400; stale product cannot overwrite price; no selected order gives visible validation.
Existing60 integration/38 unit tests remain passing, including role boundaries, API contracts, FEFO, monetary rounding, transaction fault rollback and concurrency.

### WEB04 — actual browser actions
Windows host; Codex in-app browser; HTTPS https://localhost:7194; isolated demo database. No browser certificate warning appeared or was bypassed. Local setup HTTP probes used the existing test-only SkipCertificateCheck switch; application TLS validation was not changed.

Performed administrator login; inspected actual stock (yeast15 eligible +3 expired) and three pending orders. Entered5/8/9 against15: live message displayed over-stock, remaining−7, confirmation disabled. Changed to4/5/6: requested22/allocated15/shortage7 and enabled confirmation. Acknowledged finalization and submitted the real form. Confirmed three partial outcomes, lot history consuming6 early +9 later, three fulfilled sales totaling187.50 and six installments due2026-10-29/2026-11-05.

Opened Bakery B sale62.50, installment principal31.25. Server quote29.06 included2.19 early discount. Clicked Register full payment, observed Saving changes, then waited for Payment registered/State:Paid. Customer page showed remaining principal31.25/payable26.87. Receivables showed global principal156.25; Paid filter showed exactly the recorded installment. Dashboard after normal server restart showed pending0/sales187.50/open156.25/overdue0/expiring18. Cookie and financial state persisted.

Responsive inspection used wide and narrow viewport overrides, then restored the browser default. Narrow DOM had no horizontal document overflow (body312px vs viewport325px); the table alone scrolled (592px content in275px container). Browser screenshot export under viewport override cropped some right-side content; those images are actual captures, not a full-width proof. Browser console warning/error query returned[]. A screenshot helper read of unsupported `screen.width` threw; retried with supported DOM geometry. One navigation locator timed out after responsive labels changed; refreshed state and used the current label. No application error resulted.

Actual ignored captures: artifacts/verification/admin-dashboard-before.jpg, admin-allocation-invalid.jpg, admin-allocation-ready.jpg, admin-allocation-confirmed.jpg, admin-payment-registered.jpg, admin-receivables.jpg, admin-receivables-narrow.jpg, admin-dashboard-final.jpg and admin-dashboard-normal.jpg. Captures are from performed actions, with synthetic demo data only; no password screenshot was saved. Final viewport capture/cleanup is recorded below when completed.

### NOT RUN / remaining scope
- Mobile builds/runtime: NOT RUN this phase; Mobile/shared Contracts were unchanged. Previous platform builds are historical evidence only.
- Human-selected demo credential setup: NOT RUN; generated temporary credentials used for verification only.
- Formal screen-reader or keyboard-only full workflow, additional browsers, production deployment, load/power-loss and interrupted schema migration recovery: NOT RUN.
- Partial payments, group deactivation and unconsumed-lot correction UI: unsupported/deferred as recorded in BACKLOG.md.
No packages or migrations added, no production data reset, no push/deployment or next-phase implementation.

Phase 04 cleanup executed: pwsh -NoProfile -File scripts/Stop-AdminReview.ps1 returned exit0; the verified disposable process, its database and temporary credential metadata were removed. Actual normal-viewport final screenshot: artifacts/verification/admin-dashboard-viewport.jpg. Review tab closed and viewport overrides restored. Screenshots/logs preserved; no default database reset.

## Phase 05 — native MAUI salesperson application — 2026-10-22 (America/Sao_Paulo)

### Environment / independent builds
SDK10.0.401; existing MAUI10.0.20. Normal-access `dotnet workload list` succeeded: android36.1.69/10.0.100 and maui-windows10.0.20/10.0.100 (plus existing Apple workloads, not targeted). No workloads, licenses or large dependencies were installed. CommunityToolkit.Mvvm8.4.2 was checked against its primary NuGet page and restored successfully. Windows/Android production references still point only to Contracts.

Initial sandbox `dotnet --info`/`dotnet workload list` could not open Windows Service Control Manager; workload inspection/builds succeeded with normal SDK access. A discovery read named a nonexistent DistributionEndpoints.cs, then the actual Endpoints.cs was found. These are failed/blocked discovery commands, not missing platform prerequisites.

| Actual command / check | Result |
|---|---|
| `dotnet build src/VanderCake.Mobile/VanderCake.Mobile.csproj -f net10.0-windows10.0.19041.0 -c Debug -v minimal` | First attempt FAILED:3 errors,0 warnings. Unsupported nested x:Type StaticResource key (two XAML diagnostics) and missing generic bracket in purchases API call. Both corrected. |
| Independent Android command, `-f net10.0-android -c Debug --no-restore -v minimal` | First attempt FAILED with the same3 errors. Corrected independently, not marked PASS from Windows result. |
| Corrected Windows build | PASS,13.83s,0 warnings/errors. Raw phase05-windows-fixed.txt. |
| Corrected initial Android build | PASS,1m08.28s,0 warnings/errors. Raw phase05-android-fixed.txt. |
| Windows rebuilds following actual contrast/lifecycle findings | PASS; final `-f net10.0-windows10.0.19041.0 -c Debug --no-restore -v minimal`:10.47s,0 warnings/errors. Raw phase05-windows-verified.txt. |
| Android standalone/final rebuilds | PASS; embedded assemblies enabled after direct-install smoke failure. Final `-f net10.0-android -c Debug --no-restore -v minimal`:3m14.91s,0 warnings/errors. Raw phase05-android-current.txt (copy of the original phase05-android-release-check.txt log; command was Debug, NOT Release). |
| `dotnet test VanderCake.Server.slnx --filter FullyQualifiedName~MobileClientTests --logger 'trx;LogFilePrefix=phase05-mobile-initial' --results-directory artifacts/verification` |9 integration cases PASS; unit project had no matching cases and was NOT RUN under this filter. No unit success inferred from the filter's exit0. |
| Full suite before the last two focused cases | PASS38 unit +75 integration (113). |
| Client-only reruns after retry/lifecycle changes | PASS10 cases at each executed intermediate stage. A second lifecycle case was then added. |
| Final `dotnet test VanderCake.Server.slnx --no-restore --logger 'trx;LogFilePrefix=phase05-final' --results-directory artifacts/verification` | **PASS115/115:38 unit +77 integration,0 failed/skipped.** Unit193ms; integration1m20s. Final TRX timestamps20261022105442 and20261022105606; raw phase05-tests-final.txt. |

All builds above are local Debug builds. Release signing/distribution and Release runtime were NOT RUN. No schema migration or backend business-rule change was needed. Shared contracts gained BearerLoginResponse for the existing Identity response, so both native target builds and backend regressions were executed.

### Client tests / real server proof
MOB01: exact SalesApi/Login/Portfolio/Customer/NewOrder/History view-model sources used against the SQLite/Identity test host, including native-client-created order visible in authenticated /Admin/Orders and detail HTML.
MOB02: customer registration assigned by server, fulfilled purchase quantities/historical price and debt reads.
MOB03: endpoint-bound session restore, different-origin denial before token transmission and administrator-login rejection.
MOB04: network failure safe message, true cancellation, invalid login and local token expiry.
MOB05: four unsafe URI cases rejected (HTTP, credentials, path, query).
MOB06: exact valid local certificate pin; wrong host/protocol/pin/expiry fail closed.
MOB07: server committed an order, response deliberately lost; draft locked and same-key retry returned exactly one order.
MOB08: page navigation cancellation is silent; explicit Cancel produces feedback.

The test project links exact platform-neutral mobile source files, not copied client implementations. Memory-vault tests do not claim to prove OS secure storage; actual Windows restart below does.

### NATIVE05-WIN — actual UI and persistence
Built unpackaged EXE launched as a real native window. Initial computer-use launch approval timed out; a normal CLI launch succeeded, and subsequent approved native window inspection/input worked. The app's process remained alive and displayed its actual login screen.

Actual inspection found a dark host-theme background because ContentPage's implicit style did not apply to subclasses. Added ApplyToDerivedTypes and verified light content contrast. Also fixed Picker title/SearchBar colors. Normal keyboard typing was required for MAUI text-change bindings: UIA set_value changed native display without invoking the view-model changes, causing an initial validation error. Switched to real keyboard input and retried successfully. One generated disposable password briefly appeared in the tool accessibility output during this experiment; no real credential was used and no saved screenshot/source includes it. That disposable session/database was later removed. No password is reproduced in this log.

Performed real native login over HTTPS using the platform's normal certificate validation (no development pin entered), displayed Bakery A/B/C in visit sequence, opened A and server debt, created an order for2 yeast units/term10/2026-10-23 at19:00, saw estimated25.00 and the explicit confirmation, submitted and inspected server status Submitted with total2500 cents. Exact order:13838a18-d01c-45be-bf56-c68fa77a88fb. Isolated server query found exactly one order; authenticated real Razor /Admin/Orders contained its ID.

Using the existing administrator allocation API, explicitly approved1 of2 units in this disposable dataset. Native Refresh showed PartiallyFulfilled/requested2/fulfilled1/unfulfilled1. Customer page read principal12.50/payable11.18 from the server. Native purchase history showed one fulfilled unit at historical12.50. Automatic navigation had left a misleading cancellation notice; fixed by separating Deactivate from explicit Cancel and adding MOB08.

Stopped/rebuilt/restarted the app and restarted the same server/database normally, with no migration/seed/reset. Platform SecureStorage restored the session without credentials and opened the assigned portfolio. Native Order history displayed the persisted partial order. Confirmed sign-out, observed the login screen with empty email/password, then stopped only the verified test process.

Actual JPEGs under ignored artifacts/verification: phase05-windows-portfolio.jpg, -customer.jpg, -confirmation.jpg, -submitted.jpg, -fulfilled.jpg, -purchases.jpg, -restored.jpg, -history.jpg and -login.jpg. Raw supporting summaries: phase05-windows-runtime.txt, phase05-native-server-order.txt, phase05-native-admin-proof.txt. Images are unmodified captures of performed actions, not mockups.

One screenshot-format check assumed PNG and failed before saving/clicking; observed JPEG metadata and saved the original JPEG instead. One scroll call omitted scrollX and was rejected; retried with both axes. An offscreen submit control had no cached bounds; scrolled and reobserved before acting. These automation errors did not become application success evidence.

### NATIVE05-ANDROID — actual startup smoke, not interactive workflow
`adb devices -l` initially showed no connected devices. Existing AVD VanderBeer_API35 was available; `emulator -accel-check` returned WHPX installed/usable. Started that AVD with -read-only -no-snapshot-save -no-window -no-audio, preserving its saved data/snapshot. No physical device was used.

First direct Debug APK install succeeded and am start returned Status:ok, BUT the process then aborted with SIGABRT: no assemblies found, assuming Fast Deployment. The resumed activity was the launcher. This attempt is **FAIL**, despite build/install/start command success. Added Android EmbedAssembliesIntoApk=true, rebuilt the standalone APK and reinstalled.

Corrected APK: install Success, cold launch4414ms, live process4041 and topResumedActivity MainActivity verified. Final APK after all code changes was installed again, force-stopped/relaunched: Status:ok, cold launch4855ms, live process4456 and resumed com.vandercake.mobile/crc64be48ef10161b944d.MainActivity. Scoped app log had no managed/AndroidRuntime fatal; a graphics format fallback warning was observed on the first corrected launch. Evidence: phase05-android-launch-diagnostics.txt retains the failed Fast Deployment attempt; phase05-android-smoke-final.txt and phase05-android-process-final.txt retain final actual checks.

**PASS scope: Android standalone package install and native startup only. Android interactive login, HTTPS pin entry, customer/order touch workflow and physical-device runtime: NOT RUN.** No Android screenshot or end-to-end success is claimed.

### Cleanup / remaining checks
Native sign-out cleared saved session and revoked server sessions. The verified native test process was stopped; verified emulator-5554/VanderBeer_API35 was shut down without saving a snapshot. Stop-AdminReview.ps1 returned exit0 and removed only the disposable server/database/generated credential metadata. Default persistent application database was not reset. Logs/TRX/screenshots retained under ignored artifacts. Non-secret API endpoint preferences remain configurable on login.

Human-selected credentials and final combined university rehearsal remain pending. Native registration is client/server-tested; its form was not manually submitted in this native session. Formal accessibility/screen-reader/full keyboard-only audit, large portfolios, other Android API levels, physical devices, Release distribution and interrupted-process uncertain-request recovery are NOT RUN. No offline queue, partial payment, next-phase work, git push or deployment was added.

Final Phase05 static verification: parsed TRX confirms38/38 unit and77/77 integration,0 failed/notExecuted; Mobile references Contracts only; merged Android manifest usesCleartextTraffic=false; no unignored database/key/log/TRX/generated session credentials; disposable metadata absent. Evidence: artifacts/verification/phase05-static.txt.

## Phase06 — final QA, integration and delivery review (2026-10-22)
Environment: Windows, America/Sao_Paulo; SDK10.0.401. All results below were actually executed. Raw files are ignored under artifacts/verification. Earlier phase evidence remains historical; fresh HTTP checks are not visual/native runtime claims.

| Command / check | Expected | Observed | Evidence |
|---|---|---|---|
| dotnet build VanderCake.slnx --no-restore -c Debug -v minimal | Backend and both native targets compile | Exit0 before changes and final; final0 warnings/errors,4.75s | phase06-solution-build.txt; phase06-solution-build-final.txt |
| dotnet test VanderCake.Server.slnx --no-restore -c Debug --logger 'trx;LogFilePrefix=phase06-final' --results-directory artifacts/verification (first attempt) | All tests compile/run | Exit1:38 unit passed, integration compilation CS7036 in new AcademicSeedTests: missing dispatchDate/today arguments to InventoryLot.Consume. No tests removed | phase06-tests-initial-compile-failure.txt; unit TRX...112142 |
| Same test command after correction | All116 pass | Exit0;38 unit (90ms),78 integration (45s),0 failed/skipped; new repeat-safe seed regression passes | phase06-tests.txt; phase06-final_net10.0_20261022112210.trx; phase06-final_net10.0_20261022112257.trx |
| pwsh -NoProfile -File scripts/Verify-Persistence.ps1 -CompleteWorkflow | Migrate/seed isolated DB, complete workflow, normal restart retains data/tokens | Exit0; requested2/fulfilled1; sale/AR/payment/debt and original tokens preserved; own temporary DB removed | phase06-persistence.txt; workflow-initialize.txt; workflow-restart.txt; workflow-server-first/second-stdout.txt |
| pwsh -NoProfile -File scripts/Start-AdminReview.ps1 -Mobile -AcademicFixture | New isolated fixture on7194, no initial orders | Exit0; explicit migrations/Development seed; one admin/seller, three base customers, XYZ/EX235 added | phase06-demo-setup.txt; web-review-initialize.txt |
| Prepare-AcademicDemo.ps1 -BaseUrl https://localhost:7194 (first) | Discover exactly three customers | Failed before order creation: PowerShell pipeline wrapped REST array as one element. Changed Get helper to enumerate response explicitly | phase06-demo-initial-array-failure.txt |
| Prepare-AcademicDemo.ps1 -BaseUrl https://localhost:7194 | Create/reuse XYZ5/8/9 plus EX2351 orders | Exit0 after array fix; current future dispatch2026-10-23/19:00 | phase06-demo-prepare.txt |
| Prepare-AcademicDemo.ps1 -BaseUrl https://localhost:7194 -Complete (twice) | Exact allocation/FEFO/sales/AR/payment and idempotent retry | Exit0 twice; XYZ4/5/6, consumed6+9, stock0, shortage7; XYZ sales150.00 plus235.00; eight installments; payment18.60 on20.00 principal with1.40 discount; open principal365.00. Same batch/payment IDs both runs | phase06-demo-complete.txt; phase06-demo-repeat.txt; phase06-demo-report.json |
| Actual HTTPS Invoke-WebRequest login GET/antiforgery POST, protected route GETs | Cookie login plus12 routes render real data | Exit0; /Admin,Customers,Salespersons,Products,Groups,Stock,Orders,Allocations,Sales,Receivables,AllocationHistory/{id},Sale/{id} all200 authenticated HTML | phase06-web-routes.txt |
| dotnet ef migrations list --project src/VanderCake.Infrastructure --startup-project src/VanderCake.Server --no-build | List applied schema | Exit1: Server lacks EF Design reference; incorrect tooling startup. No dependency added; use established Infrastructure design-time factory | phase06-migrations-wrong-startup.txt |
| dotnet ef migrations list --project src/VanderCake.Infrastructure --startup-project src/VanderCake.Infrastructure --no-build | Both migrations applied | Exit0;20261022002255_InitialDomainAndIdentity and20261022004717_AllocationBatchAudit; no pending marker on persistent configured default DB | phase06-migrations.txt |
| dotnet ef migrations has-pending-model-changes --project src/VanderCake.Infrastructure --startup-project src/VanderCake.Infrastructure --no-build | No model drift | Exit0, no changes since last migration | phase06-model-check.txt |
| dotnet build src/VanderCake.Mobile/VanderCake.Mobile.csproj -f net10.0-windows10.0.19041.0 -c Debug --no-restore -v minimal | Independent Windows Debug build | Exit0;0 warnings/errors,2.22s; no fresh native runtime inferred | phase06-windows.txt |
| dotnet build src/VanderCake.Mobile/VanderCake.Mobile.csproj -f net10.0-android -c Debug --no-restore -v minimal | Independent Android Debug build | Exit0;0 warnings/errors,2.82s; no interactive emulator/device claim | phase06-android.txt |
| dotnet --info; dotnet workload list; adb devices -l | Record installed readiness | SDK10.0.401, Android36.1.69/10.0.100, maui-windows10.0.20/10.0.100; no connected device. No SDK prerequisite missing | phase06-environment.txt |
| PowerShell AST parsing all scripts; git ls-files --others --exclude-standard runtime-material check; Mobile csproj reference inspection; TRX counters | Valid scripts, no exposed runtime files, Contracts-only boundary, actual counts | PASS; no unignored DB/key/log/session metadata; Mobile one production reference Contracts;38+78 PASS | phase06-static.txt |
| pwsh -NoProfile -File scripts/Stop-AdminReview.ps1 | Stop only own review process/delete own temp DB/private metadata | Exit0; metadata absent; nonsecret logs/report retained | phase06-cleanup.txt |

### Exact recorded demo
Batch ae1f3c9c-af0e-4547-9414-159c2e19858c; payment c4741c22-0b38-4e11-b613-3f45509695b1. XYZ at10.00: A4=40.00,B5=50.00,C6=60.00. Earlier lot6/later9 consumed; no XYZ stock remains. Separate EX235 sale7e223258-cd82-4fa9-8ea7-82b5ebf636ce dated2026-10-22:117.50 due2026-10-29 and117.50 due2026-11-05. Deterministic FIN02 tests separately execute2026-10-19 with26Oct/2Nov due dates. No live clock/backdating manipulation.

Before settlement total principal385.00. Paid18.60 settles20.00 principal with1.40 early discount. Remaining principal: A255.00 (including separate235.00),B50.00,C60.00,total365.00. Current payable A227.52/B44.75/C53.70 reflects the as-of22Oct discounts; principal and payable are intentionally distinct.

Repeat seed regression consumes XYZ then reruns base/academic seeds with different supplied passwords, checking unchanged users/customers/products/lots counts, stock0 and valid original login. The real two-run script preserves original allocation/payment IDs and original pre-mutation versions/keys in nonsecret LocalAppData manifests. Normal startup never reseeds.

### Reviewed code / current limits
Inspected domain InventoryLot/FEFO, allocation/finance services and SQLite workflow transaction, Identity authorization handlers, actual DTO/test/page paths and seed entry point. Current active role/stamp/portfolio checks, cents/rounding, manual quantities, transaction rollback and idempotency have executable coverage. No unrelated refactor/schema change was made.

Windows native main flow/session restoration and administrator browser visual flow retain Phase05/04 actual evidence, including phase05-windows-*.jpg and prior browser screenshots. Phase06 performed no new native launch, Android interaction or screenshot. Android interactive/physical device, Release, formal accessibility, large-data/load/power-loss/interrupted migration: NOT RUN. Group deactivation and unconsumed-lot correction: NOT IMPLEMENTED, incomplete auxiliary P0 criteria. Human chosen credentials/certificate and combined delivery-machine rehearsal: TODO.

Repository sources remain untracked (no initial commit); no push/publication/deployment. Static runtime-file exclusion is not a full secret scanner or production security certification. Material command failures above remain visible; source-inspection path misses were resolved to AuthenticationSetup.cs/DatabaseSettings.cs without code changes.

## Local manual-testing launch — 2026-10-22T14:49:24.3950490+00:00
User requested all project components running for manual testing. Explicit Development migration/base+academic seed created/reused a persistent LocalAppData/VanderCake/testing.db; generated private test credentials/session metadata are ignored under artifacts/local-run. No source credential or normal-data reset.
- Actual HTTPS health and both administrator/salesperson bearer logins: PASS.
- Portfolio: 3 customers; XYZ product available: True.
- Windows executable process started; actual process title VanderCake · Salesperson observed.
- Visible existing API35 read-only emulator booted; current Debug APK installation succeeded. Native activity launch result is recorded in this turn; no Android interactive login/order acceptance is claimed.
- Android Debug certificate pin checked against the actual server certificate using normal trusted TLS.
- Administrator page/private access file queued to Codex panels. Private passwords/tokens are not included in this log.
- Server/application/emulator intentionally left running for the user; test database is persistent. No push/deployment.

## Orientacao de execucao manual — 2026-10-23 (America/Sao_Paulo)
User screenshots show a successful full build/trusted certificate, then pwsh CommandNotFound in Windows PowerShell and a relative native executable path attempted from C:\Users\dirie instead of the repository.
Changes: Configurar-Projeto.cmd, Iniciar-Servidor.cmd, Iniciar-App-Windows.cmd; Portuguese docs/COMO_RODAR.md; README initialization now uses Windows powershell.exe and explicitly states that another terminal needs the repository directory.
Verification: actual Windows PowerShell5.1.26100.9711 parsed Initialize-Development.ps1 without syntax errors and resolved SDK10.0.401. Existing Windows native executable found. CMD wrappers statically checked for their own-folder anchor and absence of pwsh dependency; ignored runtime material remains excluded.
Evidence: artifacts/verification/manual-startup-20261023.txt.
Limits: no interactive password prompt/migration, certificate trust action, CMD launcher execution, new native runtime or Android interaction was performed in this turn. No production code/schema changes, no build/test rerun needed for these documentation/launcher additions. No running processes/data were stopped or reset.

## Sessao local recuperada e autenticada — 2026-10-23
Pedido: fazer a aplicacao funcionar para teste, apos falha na tentativa manual.
Diagnostico real: inicialmente nao havia resposta observada; durante o inicio a porta7080 estava ocupada por outra instancia. A tentativa de iniciar nessa porta falhou com AddressInUseException. As credenciais da sessao anterior e as senhas atualmente em user-secrets retornaram401 nessa instancia. Nao foram redefinidas senhas nem encerrado esse servidor.
Recuperacao: servidor em HTTPS localhost7194 usando o testing.db existente e credenciais originais de artifacts/local-run/session.json. Health e logins API dos dois papeis: PASS. Login web real com antiforgery/cookie via HTTP:200/Dashboard.
Prova visual/UI: aplicativo Windows autenticado pelo formulario real; portfólio mostrou Bakery A/B/C com sequencias1/2/3. Na primeira tentativa UI, botao foi acionado sem senha e exibiu validacao; posteriormente a senha foi digitada no campo protegido e o login concluiu. Browser IAB autenticado no Dashboard; aba marcada para permanecer aberta. Edge nao estava disponivel pelo conector; IAB foi usado.
Artefatos: artifacts/verification/local-testing-portfolio-20261023.jpg e local-testing-admin-20261023.png. Logs de tentativa7080 em artifacts/local-run/server-stdout/stderr.txt; servidor ativo7194 em testing-server-stdout/stderr.txt. Credenciais/saida privada do iniciador permanecem apenas em artifacts/local-run ignorado, nao neste log.
Facilidade de reinicio: Abrir-Teste.cmd/Parar-Teste.cmd e scripts/Local-Testing.ps1 adicionados; docs/COMO_RODAR.md atualizado. Iniciador executado realmente em Windows PowerShell5.1 com -NoBrowser -NoApp: reutilizou servidor7194 e validou ambos os logins. Caminho de encerramento nao executado; usuario precisa da sessao ativa. Sem alteracao C#/schema, sem redefinicao/limpeza de dados ou push. Android interativo nao foi testado nesta sessao.

## Preparacao da publicacao no GitHub — 2026-10-23 (America/Sao_Paulo)
Pedido explicito do usuario: enviar o projeto para https://github.com/k11ngp1ng/maui-app-vandercake.
Verificacao: git ls-files --others --exclude-standard enumerou 221 arquivos; verificacao de caminhos de runtime/configuracao e busca direcionada de padroes de credenciais encontraram zero ocorrencias (exit 0). git check-ignore confirmou exclusao de artifacts/local-run/session.json, artifacts/verification e banco de teste. Evidencia: artifacts/verification/github-prepublish-20261023.txt (ignorado).
Destino: conector GitHub confirmou repositorio publico, vazio e permissao de push. git ls-remote --symref inicialmente falhou por DNS no sandbox; repeticao com acesso de rede autorizado terminou exit 0, sem HEAD remoto. Publicacao sera feita por Git para incluir os recursos binarios junto ao codigo.
Limites: verificacao estatica direcionada, nao auditoria completa de segredos. Nenhuma alteracao de codigo de producao, nova fase, build, teste automatizado ou execucao de UI nesta tarefa; evidencias anteriores permanecem validas apenas para os escopos registrados. Bancos, credenciais, logs e compilados ficam locais. Envio ao GitHub nao implanta o servidor.
Confirmacao: git push -u origin main terminou exit 0; git ls-remote origin refs/heads/main confirmou o commit inicial 09b233eec5af5f8c273b7581ca04806e6a17914c igual ao HEAD local, com 221 arquivos versionados e arvore limpa. Evidencia: artifacts/verification/github-publish-20261023.txt. A tentativa inicial de git add no sandbox falhou por acesso negado a .git/index.lock; repeticao com permissao autorizada concluiu commit/configuracao/envio. Repositorio publicado; nenhum deploy ou build distribuido.
