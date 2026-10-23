# ADR 001 — One server, native client, simplified Clean Architecture

- Date: 2026-10-21 (America/Sao_Paulo)
- Status: accepted for Phase 01; business assumptions remain explicitly adjustable.
- Context: empty repository, university deadline 2026-10-28, approximately 5–10 development hours.

## Decision
Use the installed stable .NET SDK 10.0.401, pinned in global.json with latestPatch roll-forward. Target net10.0 for server/libraries/tests; native MAUI targets net10.0-android and net10.0-windows10.0.19041.0. Pin MAUI Controls 10.0.20 to the installed MAUI workload; EF Core/ASP.NET testing packages 10.0.12 match the installed runtime family.

Use Domain, Application, Contracts, Infrastructure, Server, Mobile, UnitTests and IntegrationTests. Host minimal HTTP API endpoints and Razor Pages together. Register EF Core SQLite in the server process only. MAUI uses XAML, MVVM and constructor injection. Test with xUnit. No business modules in Phase 01.

Use a full solution and a server-only solution rather than making backend tests depend on platform workloads. Keep dependencies explicit; no mediator, mapper or generic repository framework.

## Next-phase security and consistency choices
Use ASP.NET Core Identity with administrator cookies and native bearer tokens protected by the built-in bearer handler. Keep token issuance/login deliberately scoped; do not expose default public registration. Persist Data Protection keys outside source control for a deployed server. Use role policies and portfolio checks. All sales and stock changes go through application services.

Use integer cents in SQLite and decimal arithmetic in domain rules. Use application-managed concurrency tokens and a single transaction for fulfillment, stock, sale and receivables. Use full-installment settlement for P0; partial payments are P1.

## Alternatives and consequences
- Separate admin app/SPA or microservices: rejected; adds deployment and authentication work without helping the deadline.
- SQLite in MAUI/offline synchronization: rejected; conflicts with server ownership and creates conflict resolution work.
- .NET 8/9 downgrade: unnecessary; installed .NET 10 builds are verified in TEST_LOG.md.
- Full enterprise Clean Architecture abstractions: rejected; add interfaces only at real use-case boundaries.
- SQL Server: unnecessary for the single-server academic demo. SQLite means limited write concurrency and careful provider-specific tests.
- No production deployment or store packaging in this phase. Local builds do not prove device/runtime behavior.

## References checked
- [MAUI installation](https://learn.microsoft.com/dotnet/maui/get-started/installation)
- [MAUI supported platforms](https://learn.microsoft.com/en-us/dotnet/maui/supported-platforms?view=net-maui-10.0)
- [EF Core SQLite 10.0.12](https://www.nuget.org/packages/Microsoft.EntityFrameworkCore.Sqlite/10.0.12)
- [SQLite provider limitations](https://learn.microsoft.com/en-us/ef/core/providers/sqlite/limitations)
- [Identity API authentication](https://learn.microsoft.com/en-us/aspnet/core/security/authentication/identity-api-authorization?view=aspnetcore-10.0)

Local command evidence, rather than documentation alone, determines build readiness.

