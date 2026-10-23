# Phase 02 completion report

Date: 2026-10-21, America/Sao_Paulo. Phase 03 has not started.

## Completed work
- Domain entities/relationships for all requested models. Standard Identity user/role entities are Infrastructure-owned; domain remains independent of ASP.NET/EF.
- Cents-based money, basis-point percentages, deterministic rounding/cent remainders, quantities/date/price/stock validation, explicit states and concurrency versions.
- EF Core SQLite mapping, FK/unique/check constraints, indexes, seeded reference data and generated migration 20261022002255_InitialDomainAndIdentity.
- Initial migration applied to the default persistent LocalAppData server database; no pending model changes.
- Application service/persistence/current-user abstractions; customer create/read/update, product create/read/update, order submission/read and reference APIs.
- Identity hashes/lockout, administrator cookie session/antiforgery and explicitly required protected bearer scheme for APIs.
- Current-user, role, active-account/profile, security-stamp and portfolio enforcement. Cookie/bearer schemes cannot substitute for one another.
- HTTPS API/credential enforcement, structured validation/ProblemDetails and ownership-safe 404s.
- Explicit Development-only demo seed, environment/user-secret passwords and interactive setup script. No committed passwords/signing keys.
- Documentation, API reference, test traceability/backlog and actual execution log.

## Verification
- Backend compiled successfully. Initial explicit build: zero warnings/errors; final test builds also succeeded.
- Final suite: **26 unit tests and 31 integration tests passed; zero failed/skipped**.
- After the final HTTP login-form guard, the eight affected HTTPS/admin/foundation checks passed again. Setup-script syntax, production project boundaries and source-output exclusions also passed static checks.
- Tests cover invariants, malformed/invalid input, culture-independent price validation, authentication/role/portfolio boundaries, secure cookies/antiforgery, token expiry/tampering/revocation, real SQLite constraints/concurrency, persistence and API contracts.
- Real-process HTTPS test explicitly migrated/seeded a disposable database, created an order, stopped/restarted the server normally and read the same order with the original token. Passed. Temporary database cleaned up.
- Production demo-seed attempt refused with nonzero exit and created no database (expected guard behavior).
- MAUI Windows/Android Debug builds passed with zero warnings/errors after shared-contract changes. No native runtime or screenshot claim.
- Valid localhost development certificate found; trust was not changed or verified.

## Actual failures and correction
Initial tests: 24 unit passed; integration 18 passed/3 failed. Two failures exposed locale-dependent DataAnnotations decimal-limit parsing producing 500s. Fixed with invariant parsing and two explicit culture regression cases. The third was a test's overly narrow 404 expectation for an absent registration route; the framework returned 405, which also prevents registration. Corrected the assertion. One nullable test warning was fixed.

An initial local-tool install entry was overwritten by the scaffold command completing afterward; reinstalling into the finalized root dotnet-tools.json resolved the manifest ordering issue. The final tool restore/version is pinned to 10.0.12.

## Commands and evidence
See TEST_LOG.md for exact commands/results and artifacts/verification for raw output. Main commands:
- dotnet build VanderCake.Server.slnx --configuration Debug --nologo
- dotnet tool install dotnet-ef --version 10.0.12
- dotnet ef migrations add InitialDomainAndIdentity --project src/VanderCake.Infrastructure --startup-project src/VanderCake.Infrastructure --output-dir Persistence/Migrations --no-build
- dotnet ef database update --project src/VanderCake.Infrastructure --startup-project src/VanderCake.Infrastructure --no-build
- dotnet ef migrations has-pending-model-changes --project src/VanderCake.Infrastructure --startup-project src/VanderCake.Infrastructure --no-build
- dotnet test VanderCake.Server.slnx --configuration Debug --nologo --logger trx --results-directory artifacts/TestResults/phase02-final
- pwsh -NoProfile -File scripts/Verify-Persistence.ps1
- dotnet build src/VanderCake.Mobile/VanderCake.Mobile.csproj -f net10.0-windows10.0.19041.0 --configuration Debug --nologo
- dotnet build src/VanderCake.Mobile/VanderCake.Mobile.csproj -f net10.0-android --configuration Debug --nologo

## Remaining dependencies and scope
No unresolved build/migration/package dependency. Before interactive login, run Initialize-Development.ps1 to choose known passwords and trust the local HTTPS certificate. The normal DB contains the migrated reference schema; automated demo users/data were exercised in isolated databases, not installed with undocumented shared credentials.

Core sale/payment/lot models exist; operational stock/allocation/FEFO-to-sale/receivables/payment workflows, remaining admin masters/screens, history/debt/dashboard and native authentication/screens remain in BACKLOG.md. Those are not claimed as complete. The native app still displays its scaffold.

Production requires standards-based OIDC/OAuth2 identity, validated access tokens, secure HTTPS and durable protected key storage. This phase's opaque-token setup is for the local academic demonstration.

No git push, deployment, public publication or Phase 03 work occurred.
