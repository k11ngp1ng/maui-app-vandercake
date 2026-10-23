# Ordered academic presentation — Phase06

Use a dedicated fixture outside OneDrive. Setup and scripted completion were executed in an isolated database on2026-10-22; human-password setup below remains a delivery-machine task. Never delete a normal database or replenish consumed lots to repeat the demo.

## 1. Prepare privately (before presentation)
From repository root, PowerShell7:
```powershell
dotnet tool restore
dotnet build VanderCake.slnx -c Debug
dotnet dev-certs https --trust
# Choose a NEW filename for a fresh fixture; retain the same value for server restarts.
$env:ConnectionStrings__VanderCake = "Data Source=$env:LOCALAPPDATA/VanderCake/academic-delivery-20261028.db;Foreign Keys=True"
pwsh -NoProfile -File scripts/Initialize-Development.ps1 -AcademicFixture
dotnet run --project src/VanderCake.Server --launch-profile https
```
Setup prompts for chosen administrator/salesperson passwords through user-secrets, never command-line arguments. Accounts: admin@vandercake.local, salesperson@vandercake.local. Existing credentials are preserved on reseed. Keep the database connection override in the server terminal. If the filename already contains a completed fixture, inspect it; choose another new filename for a new rehearsal without deleting old data.

Base seed: one administrator, one salesperson, three assigned customers A/B/C with visit orders1/2/3, four bakery categories/products and lots/terms10/20. Academic opt-in adds XYZ at BRL10.00 with15 valid units across earlier6/later9 lots, and EX235 at BRL235.00 with1unit. Repeating seed does not duplicate records, reset passwords or refill stock. Lots/requests age with calendar time; use current future dispatch and a new dedicated fixture when needed.

## 2. Choose ONE rehearsal mode
**Manual presentation:** in another terminal:
```powershell
pwsh -NoProfile -File scripts/Prepare-AcademicDemo.ps1
```
It privately prompts for the existing passwords, authenticates through HTTPS, and creates/reuses four submitted orders: XYZ5/8/9 and separate EX2351. These are real API-created orders, not evidence of native data entry.

**Automated acceptance rehearsal:** run the same preparation, then:
```powershell
pwsh -NoProfile -File scripts/Prepare-AcademicDemo.ps1 -Complete
pwsh -NoProfile -File scripts/Prepare-AcademicDemo.ps1 -Complete
```
Complete explicitly applies4/5/6 and EX2351 in one batch, settles one XYZ installment, verifies financial/stock outcomes and reports original IDs on retry. It uses existing application services through APIs; no customer priority or scheduled job exists. Do not run Complete after manually finalizing these orders: it deliberately refuses to invent a new allocation request for already-finalized data.

Request manifests and nonsecret result reports: %LOCALAPPDATA%/VanderCake/demo-manifests/academic-{customerId}.json and report-{customerId}.json. Original payloads/versions/keys are saved before mutations; preserve them after an uncertain response. Do not delete manifests to force a retry. If an uncommitted dispatch or payment quote has expired, inspect server state and deliberately replan/requote; no silent stale-quote retry.

## 3. Show salesperson and administrator roles
Launch Windows native:
```powershell
& './src/VanderCake.Mobile/bin/Debug/net10.0-windows10.0.19041.0/win-x64/VanderCake.Mobile.exe'
```
Use https://localhost:7080 and the chosen salesperson credentials. Show assigned portfolio/sequence, detail/debt, purchase history and order statuses. Native order entry/confirmation was performed in Phase05; do not label the script-created orders as native submissions. A separate YEAST order/customer-registration demonstration may be performed after the exact fixture totals are recorded; it changes the fixture.

Administrator: https://localhost:7080/Account/Login. Show dashboard and stock; XYZ physical/eligible15, lots6+9 with valid expiration. Show three XYZ pending orders, total requested22, plus the separate EX235 order.

## 4. Perform explicit manual cutting (manual mode only)
In Allocations select the three XYZ orders, choose a still-future common date and12:00 or19:00, and enter A4/B5/C6. Demonstrate an over-stock/over-request validation error, correct it, acknowledge and confirm. Never infer customer priority. Requested22, fulfilled15, shortage7=(1+3+3). In allocation history show actor/time, lots consumed6+9 FEFO, three partial orders, sales40.00/50.00/60.00, six installments and XYZ stock0.

Confirm EX235 separately for1unit; sale235.00, term10 yields117.50 at sale date+7 and117.50 at+14. No backdated production endpoint exists.

## 5. Show installments, settlement and debt
Before payment all four sales total385.00 and eight installments total385.00. Register full settlement of BakeryA's first XYZ installment, principal20.00. On the sale date it is7days early: discount1.40, payable18.60; retain receipt components. Outstanding original principal then A255.00 (including235 example), B50.00, C60.00; combined365.00. As-of payable differs because discounts/late charges depend on today's date.

Actual automated rehearsal sale date2026-10-22:235 example due2026-10-29 and2026-11-05,117.50 each. Deterministic unit/integration tests separately prove sale date2026-10-19 ->2026-10-26 and2026-11-02. Do not imply the live server clock was backdated.

Show native partial status, historical fulfilled quantities/prices, payment receipt, reduced debt and dashboard. A repeat of the same request must return original IDs without another sale/charge; tests also reject changed content/finalized edits/other portfolios.

## 6. Persistence and final checklist
Stop only the server you started (Ctrl+C), rerun the same launch command in the same configured terminal, and inspect the same sales/payment/debt. Never pass migrate/seed between normal restarts. Automated proof:
```powershell
pwsh -NoProfile -File scripts/Verify-Persistence.ps1 -CompleteWorkflow
```
This creates/deletes only its own disposable database and verifies original tokens plus financial records across processes.

- Confirm fresh dispatch/expiry, chosen credentials, trusted HTTPS and correct database before presenting.
- Keep test/build logs and requirement coverage available; state unsupported features openly.
- Windows native and web visual evidence exists from prior phases; fresh Phase06 HTTP checks are not new screenshots.
- Android build/startup are verified; interactive flow, physical devices, formal accessibility and Release packaging remain NOT RUN.
- Partial payments, group deactivation and unconsumed-lot correction are unavailable; do not present placeholders.
- Do not publish credentials, tokens, databases or ignored session metadata.

Optional disposable helpers: Start-AdminReview.ps1 -Mobile -AcademicFixture starts a new temporary HTTPS fixture on7194 with generated private ignored metadata; pass -BaseUrl https://localhost:7194 to Prepare-AcademicDemo. Stop-AdminReview.ps1 removes only that helper's process/database/session metadata. Normal presentation should use chosen persistent configuration above.
