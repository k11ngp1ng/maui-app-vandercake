# VanderCake engineering rules

## Scope and delivery
- Read docs/REQUIREMENTS.md, docs/BUSINESS_RULES.md, docs/BACKLOG.md and docs/TEST_LOG.md before a new phase. Follow the current user's phase boundary; never start another phase automatically.
- University deadline: 2026-10-28, America/Sao_Paulo. Total practical budget: roughly 5–10 hours. Deliver the P0 vertical flow first; P1 is deferred unless time remains and the user authorizes it.
- Keep one server deployment and one server-owned SQLite database. No microservices, CQRS frameworks, message brokers, extra frontend frameworks or speculative generic repository layers.
- Do not push, deploy, publish or distribute builds without explicit authorization. Do not overwrite unrelated user changes.

## Boundaries
- Domain: pure C# business entities, value objects and rules. No EF, HTTP, MAUI or UI dependencies.
- Contracts: transport DTOs/enums and basic input constraints; no entities or EF types.
- Application -> Domain, Contracts. Own use cases and persistence/current-user abstractions; no server/UI dependencies.
- Infrastructure -> Application, Domain. Own EF mappings, migrations, persistence and Identity adapters.
- Server -> Application, Infrastructure, Contracts. Composition root; API and Razor Pages share application services. Razor handlers must not call the server's own HTTP API.
- Mobile -> Contracts ONLY among production projects. Use native MAUI XAML + MVVM, injected services, typed HttpClient and SecureStorage when authentication is implemented. Never reference EF, SQLite, Infrastructure, Application or Domain in Mobile.
- Tests may reference their subject. Preserve these project boundaries when adding packages.

## Correctness
- Server is authoritative for ownership, prices, terms, dispatch slots, stock, fulfillment and balances.
- Enforce role AND portfolio ownership in server queries/use cases, including history and debt. Never trust a client-supplied salesperson ID.
- Validate DTO shape at the boundary and invariants in domain/application services. Use ProblemDetails/ValidationProblemDetails and meaningful status codes; never return stack traces or secrets.
- Use asynchronous I/O and CancellationToken propagation. No .Result/.Wait(), async void except UI event boundaries, or Task.Run around database calls.
- Inject TimeProvider. Store audit timestamps in UTC; business dates use configured America/Sao_Paulo semantics (Windows equivalent if needed).
- BRL assumption: decimal calculations, explicit rounding, integer cents in SQLite; never binary floating point for money. Quantities are integer sale units in P0.
- Administrator chooses allocations. Never invent automatic customer priority. Consume only valid lots using FEFO, then deterministic tie breaks.
- Revalidate and commit allocation, lot consumption, sale and receivables atomically. Prevent negative stock, fulfillment above requested, concurrent overselling and duplicate financial records.
- Use application-managed concurrency tokens with SQLite and database uniqueness constraints. Return 409 for stale state; never silently retry stale allocations.
- Financial examples and settlement limits are defined in BUSINESS_RULES.md; do not silently change them.

## Security and storage
- No hardcoded/demo passwords, tokens, signing keys or committed databases. Bootstrap credentials must come from user-secrets or environment variables.
- Use ASP.NET Core Identity password hashing; cookie auth + antiforgery for the admin website; protected bearer tokens for native API access. No public registration; account creation is administrative.
- Keep health anonymous and minimal. Add authorization before exposing any business endpoint/page.
- EF migrations are the schema source of truth once Phase 02 starts. No EnsureCreated/Migrate mixed strategy and no automatic production migration on startup.
- SQLite file belongs to the server. Before using real data, move it outside OneDrive synchronization and configure the connection string. Do not add offline client databases.
- HTTPS for authenticated use. Any local Android HTTP exception must be Debug-only and scoped; never disable certificate validation globally.

## Evidence and maintenance
- Pin stable, compatible package versions and the SDK. Change versions intentionally and record why.
- Run checks appropriate to the change. Prioritize money rounding, FEFO, authorization, concurrency and transactions; use real SQLite integration tests, not EF InMemory for relational behavior.
- Never fabricate successful builds, tests, screenshots or device runs. Build success does not prove runtime UI success.
- Update TEST_LOG.md with command, date, exit/result, count, limitations and evidence paths, including failures and blockers. Put raw output under ignored artifacts/verification; retain concise evidence in docs.
- Keep requirement IDs, acceptance criteria, planned test IDs and backlog statuses synchronized. "Planned" is not "tested".
- Use dotnet CLI / Visual Studio 2026 for .NET 10. VanderCake.Server.slnx allows backend work without mobile workloads.
- Do not install large platform workloads or accept SDK licenses merely to hide a blocker; record missing prerequisites and the exact remediation. Routine restore of referenced packages is part of building.

