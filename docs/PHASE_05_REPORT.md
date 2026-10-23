# Phase 05 delivery — native salesperson application

Completed 2026-10-22, America/Sao_Paulo. Stop at this phase; no next-phase implementation, push or release distribution.

## Implemented pages
Native XAML login; assigned/searchable portfolio; customer detail/visit order/server debt; previous confirmed purchases/quantities/historical prices; customer registration with seeded state/city choices; order entry with products/whole quantities/estimated total/server-defined terms/date/window; order history/status filter and detailed requested/fulfilled/unfulfilled status.

CommunityToolkit.Mvvm 8.4.2 supplies ObservableObject/async commands. DI provides one reusable HttpClient/API service, endpoint settings, secure token vault, navigation/dialogs and transient page view models. Shell supports back navigation and warns before leaving uncertain submissions. Views contain UI lifecycle/query wiring; the shared DTOs and backend services remain authoritative.

No client database or production project reference beyond Contracts. No backend domain rules, FEFO, penalties or installment policies were duplicated. Order entry totals are estimates; final values come from the server. Original payload/key is retained after uncertain responses to support idempotent retry. Drafts are memory-only.

HTTPS-only origins are editable at login. Windows defaults to localhost; detected virtual Android uses 10.0.2.2; physical Android requires its own reachable endpoint. Debug offers explicit exact local-certificate SHA-256 pinning, restricted to localhost/10.0.2.2 and certificate validity/identity; Release excludes that callback. SecureStorage stores only endpoint-bound access token/expiry, never passwords or refresh tokens. See MOBILE.md for setup and limits.

## Files
- Added Mobile/Client/{Abstractions,ApiConnection,SalesApi}.cs and Services/{PlatformServices,MoneyConverter}.cs.
- Added ViewModels/{PageViewModel,LoginViewModel,PortfolioViewModel,CustomerViewModel,RegistrationViewModel,NewOrderViewModel,OrdersViewModel}.cs.
- Added XAML/code-behind pairs under Views/: LoginPage, PortfolioPage, CustomerPage, PurchasesPage, RegistrationPage, NewOrderPage, OrdersPage, OrderPage, FeedbackView; plus PageLifecycle.cs.
- Updated MauiProgram, App/AppShell, styles/colors, vector icon/splash, Android manifest and csproj. Removed foundation MainPage/MainViewModel placeholder files.
- Added shared BearerLoginResponse; Mobile continues referencing Contracts only.
- Added MobileClientTests.cs; integration project links exact platform-neutral client/VM sources and pins Toolkit8.4.2.
- Extended Start-AdminReview.ps1 with disposable -Mobile fixture mode; normal application data untouched.
- Added MOBILE.md and this report; updated architecture, requirements, backlog, test plan/log, demo and README.

## Actual validation
- Windows final Debug build: **PASS**, 0 warnings/errors,10.47s.
- Android final standalone Debug build: **PASS**, 0 warnings/errors,3m14.91s.
- Final automated suite: **115 passed** (38 unit +77 integration),0 failed/skipped. Eleven added client cases cover flow/admin visibility, registration/ownership/history/debt, session/origin/role checks, cancellation/network/expiry, unsafe addresses, certificate pin bounds, lost-response retry and lifecycle feedback.
- Windows real native UI: login -> assigned portfolio -> detail/debt -> new order -> explicit confirmation -> Submitted. The real administrator website contained this exact order. An explicit one-unit allocation updated native partial status, shortage, purchase history and debt.
- Native app and server restarted normally: secure session restored without credentials and the order remained in history. Sign-out returned to blank-password login.
- Android real emulator: existing API35 AVD in read-only/headless mode, APK install, cold launch, live process and resumed MainActivity verified. **Interactive Android flow and physical devices NOT RUN.**

First builds failed on XAML/generic syntax and were corrected. Initial direct Android install aborted because Debug fast deployment omitted assemblies; embedded standalone packaging fixed it. Runtime found and corrected page contrast and misleading navigation cancellation feedback. TEST_LOG.md preserves these failures, commands, actual counts and evidence paths.

## Remaining requirements / next steps
No missing installed build dependency. Android interactive HTTPS/login/customer/order verification and physical-device connectivity remain the main acceptance gaps. Human credentials/trusted endpoint, full presentation rehearsal, formal accessibility/keyboard audit and Release packaging are still pending. Existing P1 gaps include partial payments, lot corrections/group deactivation, large-list pagination and offline/persistent drafts.

Generated verification credentials/database were removed after native sign-out; temporary app/emulator/server stopped; actual screenshots/logs remain ignored artifacts. No production certificate validation bypass, cleartext traffic, schema change, automatic jobs, push or deployment.

