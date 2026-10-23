# Development setup — Phases 02/03

## Prerequisites
.NET SDK 10.0.401 and ASP.NET runtime 10.0.12 are installed. EF/Identity/tool packages are pinned to 10.0.12. A valid localhost HTTPS development certificate was found; trust was not changed or asserted by this phase.

From the repository root:
```powershell
dotnet tool restore
dotnet build VanderCake.Server.slnx --configuration Debug
pwsh -NoProfile -File scripts/Initialize-Development.ps1
dotnet dev-certs https --trust
dotnet run --project src/VanderCake.Server --launch-profile https
```

Initialize-Development prompts securely for administrator and salesperson passwords, stores them via the development user-secrets store using stdin, then explicitly migrates and seeds. Passwords need at least 12 characters with uppercase/lowercase, number and symbol. No password or signing key is committed. ASP.NET Core user-secrets is a local development convenience, not an encrypted production vault.

Logins:
- Administrator email: admin@vandercake.local at https://localhost:7080/Account/Login
- Salesperson email: salesperson@vandercake.local through POST /api/auth/login
- Passwords: the values you entered during setup.

Existing users' passwords are never silently reset by reseeding. Use Identity tooling/a deliberate password-reset workflow if needed; changing the seed configuration does not replace an existing hash.

## Database location and commands
Default: current user's LocalApplicationData/VanderCake/vandercake.db. On this machine the persistent default schema was applied outside OneDrive. No demo credentials were saved there during automated verification; run the interactive setup above to provision known development logins.

Override ConnectionStrings__VanderCake or ignored Server/appsettings.Local.json if needed, choosing a server-owned path outside synchronization. The design-time factory reads the environment connection override, not appsettings.Local.json; use the environment override for CLI migration commands to guarantee the same target.

```powershell
dotnet ef database update --project src/VanderCake.Infrastructure --startup-project src/VanderCake.Infrastructure
dotnet ef migrations has-pending-model-changes --project src/VanderCake.Infrastructure --startup-project src/VanderCake.Infrastructure
```

Equivalent explicit app commands:
```powershell
$env:ASPNETCORE_ENVIRONMENT = 'Development'
dotnet run --project src/VanderCake.Server --no-launch-profile -- --migrate
dotnet run --project src/VanderCake.Server --no-launch-profile -- --seed-demo
```
The seed command requires Demo:AdminPassword and Demo:SalespersonPassword through user-secrets or Demo__AdminPassword/Demo__SalespersonPassword environment variables. It refuses non-Development environments. Both commands exit after the requested action. Normal startup never resets, migrates or seeds the database.

Phase 03 adds AllocationBatchAudit after the initial migration. Both are applied to the default server schema. SQLite rebuilds Orders for its new FK and EF emitted a nontransactional-PRAGMA warning; stop the app and keep a consistent backup before future schema maintenance. Legacy-data upgrade preservation passed; interruption during migration is NOT RUN.

A brand-new database must be migrated before business requests. The health endpoint remains a liveness probe and intentionally does not prove schema readiness.

## Verification
```powershell
dotnet test VanderCake.Server.slnx --configuration Debug --logger trx --results-directory artifacts/TestResults
pwsh -NoProfile -File scripts/Verify-Persistence.ps1
pwsh -NoProfile -File scripts/Verify-Persistence.ps1 -CompleteWorkflow
```
Verify-Persistence uses a uniquely named disposable database under the OS temp directory, generated in-memory credentials and two real HTTPS processes. It verifies that an order AND the original protected access token survive restart. It skips certificate trust only for those localhost test requests, stops its own processes and deletes only its temporary files. It never modifies or resets the normal database. Raw logs contain no generated passwords.

The interactive password-prompt script was inspected, not executed with human-chosen credentials. Its migration/seeder paths were exercised by tests and the process verification using generated credentials.

## Security/deployment limits
The local demo uses ASP.NET Core protected opaque bearer tokens, not JWTs, and no public registration/refresh endpoint. Production requires a standards-based OpenID Connect/OAuth2 provider, validated tokens, secure HTTPS and durable protected key management. See ADR-002.

Basic administrator workflow pages are available at /Admin/Orders and /Admin/Receivables; both use the same services as the API. WORKFLOW.md contains the complete allocation/payment contract and configuration guide. Native authentication/screens and complex master-management UI remain pending; MAUI still displays its foundation screen.
