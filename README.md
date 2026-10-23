# VanderCake

Academic B2B bakery distribution system. Deadline: **2026-10-28**. Final QA performed **2026-10-22**.

Implemented: native salesperson MAUI app, administrator Razor Pages, HTTPS API and server SQLite; order -> explicit manual allocation -> FEFO -> fulfilled sale -> installments -> full payment. **116 tests pass (38 unit, 78 integration).** Full Debug solution build passes, including Windows and Android. Windows native and web visual flows have prior runtime evidence; Android has build/startup evidence only. Delivery is conditional on the remaining acceptance gates in [the final report](docs/PHASE_06_REPORT.md).

## Prerequisites and launch
Windows 10 build19041+ / Windows11; .NET SDK **10.0.401** (global.json); ASP.NET Core10 runtime. Native builds require Visual Studio2026/build tools, MAUI Windows10.0.20, Android workload36.1.69, Android SDK and JDK21. Android runtime requires an emulator/device and reachable HTTPS. These SDK/build dependencies are installed on the reviewed machine; no physical Android device was attached.

For Windows PowerShell5.1 or PowerShell7, use the repository root. Portuguese step-by-step: [COMO_RODAR](docs/COMO_RODAR.md). You can also double-click Configurar-Projeto.cmd once, then Iniciar-Servidor.cmd and Iniciar-App-Windows.cmd.
```powershell
dotnet tool restore
dotnet build VanderCake.slnx -c Debug
dotnet dev-certs https --trust
powershell.exe -NoProfile -ExecutionPolicy RemoteSigned -File scripts/Initialize-Development.ps1 -AcademicFixture
dotnet run --project src/VanderCake.Server --launch-profile https
```
Setup privately prompts for passwords and stores development user-secrets; it explicitly migrates/seeds. Existing passwords and consumed stock are preserved. Admin: https://localhost:7080/Account/Login, admin@vandercake.local. Salesperson: salesperson@vandercake.local. Use your chosen passwords.

Default database: %LOCALAPPDATA%/VanderCake/vandercake.db, outside OneDrive. Normal startup never resets, migrates or seeds. For a fresh rehearsal, select a **new** database filename using ConnectionStrings__VanderCake before setup and server launch; see [DEMO](docs/DEMO.md). Never delete/reset existing data to rerun a demo.

Native Windows, from another terminal opened in the repository root after building (each new terminal starts in its own directory):
```powershell
& './src/VanderCake.Mobile/bin/Debug/net10.0-windows10.0.19041.0/win-x64/VanderCake.Mobile.exe'
```
Set https://localhost:7080 in native login settings. Android emulator uses https://10.0.2.2:7080; physical devices need a separately reachable HTTPS endpoint. [MOBILE](docs/MOBILE.md) covers local certificate pinning, deployment and platform limitations. Debug pinning is exact and local; Release has no custom TLS bypass.

## Reproducible acceptance
With the seeded server running:
```powershell
pwsh -NoProfile -File scripts/Prepare-AcademicDemo.ps1
pwsh -NoProfile -File scripts/Prepare-AcademicDemo.ps1 -Complete
# Repeat -Complete safely using the saved original request manifest.
dotnet test VanderCake.Server.slnx --no-restore -c Debug --logger trx --results-directory artifacts/verification
pwsh -NoProfile -File scripts/Verify-Persistence.ps1 -CompleteWorkflow
```
The demo prompts privately for existing credentials. Prepare creates/reuses XYZ orders5/8/9 and a separate235.00 order. Complete explicitly applies4/5/6 plus the separate example, verifies FEFO/sales/eight installments, and settles one installment. Manifests/reports contain no credentials and live outside the repo under LocalAppData/VanderCake/demo-manifests. Scripted completion is an explicit rehearsal command, never automatic customer prioritization. See DEMO before mixing scripted and manual completion.

Correct EF tooling startup:
```powershell
dotnet ef migrations list --project src/VanderCake.Infrastructure --startup-project src/VanderCake.Infrastructure --no-build
dotnet ef migrations has-pending-model-changes --project src/VanderCake.Infrastructure --startup-project src/VanderCake.Infrastructure --no-build
```

## Architecture and limits
Eight projects: Domain, Application, Contracts, Infrastructure, Server, Mobile and two xUnit projects. VanderCake.Server.slnx excludes Mobile for backend-only development. Mobile references Contracts only. API/Razor share application services; only the server accesses SQLite. Identity uses secure admin cookies/antiforgery and protected opaque API bearer tokens with current-role/stamp/portfolio checks.

Single-server academic deployment, integer sale units, cents storage, immutable price/rate snapshots, full-installment settlement only. Group deactivation and unconsumed-lot correction remain incomplete P0 auxiliary criteria. Android interactive acceptance, human delivery-machine setup and a combined presentation rehearsal remain open. Production identity/HTTPS/key operations, partial payments, pagination, offline sync and release distribution are deferred.

[Coverage matrix](docs/REQUIREMENTS_COVERAGE.md) · [Final report](docs/PHASE_06_REPORT.md) · [Actual results/failures](docs/TEST_LOG.md) · [Backlog](docs/BACKLOG.md) · [Known limits](docs/KNOWN_LIMITATIONS.md) · [Architecture](docs/ARCHITECTURE.md) · [Business rules](docs/BUSINESS_RULES.md) · [Demo](docs/DEMO.md).
Source repository: [k11ngp1ng/maui-app-vandercake](https://github.com/k11ngp1ng/maui-app-vandercake). Local databases, credentials, build outputs and verification artifacts are excluded from source control. Repository publication does not deploy the application.

