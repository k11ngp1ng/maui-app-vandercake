# Native salesperson application — Phase 05

The MAUI application uses native XAML views, Shell navigation, CommunityToolkit.Mvvm 8.4.2, injected services and shared Contracts. It references no Domain, Application, Infrastructure, SQLite or EF package. UI edit state wraps ProductResponse; it is not another business entity model.

## Pages and use
Login -> customer portfolio -> customer detail/debt -> previous purchases or new order. Customer registration and order history are available from the portfolio. History links to requested/fulfilled/unfulfilled quantities, dispatch and server prices. Pull to refresh the portfolio or use Refresh on detail/history pages.

Use a privately provisioned salesperson account from Initialize-Development.ps1. Administrator accounts use the web portal. Passwords are cleared after attempted network sign-in; only the endpoint-bound bearer access token and expiry are stored through platform SecureStorage. No refresh token/password is saved. Expiry/revocation requires a new login. Sign-out clears local session storage and calls the existing server logout, which revokes other sessions for that account too.

## HTTPS configuration
The login page exposes the server origin. VANDERCAKE_API_URL is an optional process environment default (useful for Windows launch); a saved non-secret address takes precedence. Defaults: Windows https://localhost:7080/; a detected virtual Android device https://10.0.2.2:7080/. Physical Android defaults to an empty address and requires an explicitly reachable HTTPS endpoint. It never assumes the emulator alias works on a phone.

Only absolute HTTPS origins without credentials, path, query or fragment are accepted. The same token is never sent to a different origin. Redirects and cookies are disabled for the API HttpClient. No cleartext HTTP allowance is included in any configuration; the server also requires HTTPS for API traffic.

Windows should trust the chosen development certificate through the normal development setup. Android emulator cannot ordinarily trust the host's localhost certificate. Debug builds offer an explicit public-certificate SHA-256 pin on the login page. The callback accepts errors only when the destination is localhost or10.0.2.2, the certificate identifies localhost, is within its validity period, and its SHA-256 matches the entered pin exactly. A wrong/missing pin fails closed. The callback is excluded by #if DEBUG from Release; production uses platform TLS validation. This is not a global certificate-validation bypass.

Inspect candidate public localhost certificate fingerprints without exporting private keys:

```powershell
Get-ChildItem Cert:\CurrentUser\My |
  Where-Object Subject -eq 'CN=localhost' |
  Select-Object Subject,NotAfter,@{Name='SHA256';Expression={$_.GetCertHashString([Security.Cryptography.HashAlgorithmName]::SHA256)}}
```

Select the actual Kestrel certificate, not an arbitrary certificate with the same subject. On physical Android, use a reachable server/tunnel with a normally trusted certificate and correct DNS name; configure server binding/network access deliberately. The local-only pin does not authorize LAN certificates. No tunnel, firewall rule or public exposure was created in this phase.

Primary guidance checked2026-10-22: [Microsoft local MAUI web services](https://learn.microsoft.com/en-us/dotnet/maui/data-cloud/local-web-services?view=net-maui-10.0), [Microsoft SecureStorage](https://learn.microsoft.com/en-us/dotnet/maui/platform-integration/storage/secure-storage?view=net-maui-10.0), [CommunityToolkit.Mvvm 8.4.2](https://www.nuget.org/packages/CommunityToolkit.Mvvm/8.4.2).

## Build / local run
From the repository root:

```powershell
dotnet build src/VanderCake.Mobile/VanderCake.Mobile.csproj -f net10.0-windows10.0.19041.0 -c Debug
dotnet build src/VanderCake.Mobile/VanderCake.Mobile.csproj -f net10.0-android -c Debug
dotnet run --project src/VanderCake.Server --launch-profile https
```

Windows executable: src/VanderCake.Mobile/bin/Debug/net10.0-windows10.0.19041.0/win-x64/VanderCake.Mobile.exe. Android Debug signed APK: src/VanderCake.Mobile/bin/Debug/net10.0-android/com.vandercake.mobile-Signed.apk. The project explicitly embeds Android assemblies, so direct adb installation does not depend on IDE fast deployment. These are local Debug artifacts, not published/release packages.

On an already booted selected emulator, the executed deployment approach is:

```powershell
adb -s emulator-5554 install -r src/VanderCake.Mobile/bin/Debug/net10.0-android/com.vandercake.mobile-Signed.apk
adb -s emulator-5554 shell cmd package resolve-activity --brief com.vandercake.mobile
# Use the exact returned package/activity with adb shell am start -W -n.
```

Choose the actual device serial, not blindly emulator-5554. Build success alone does not prove runtime. Phase 05 verified Windows UI login/order/history/purchases and SecureStorage restoration. Android verified only install, cold launch, live process and resumed MainActivity on an existing read-only headless API35 emulator. Android interactive login/order flow and physical devices remain NOT RUN.

## Safe retry and server authority
Order totals on entry are explicitly estimates using the loaded catalog. Submission sends only shared CreateOrderRequest; the server controls ownership, current prices, dates, terms, final amounts and allocation. Debt/installments are displayed directly from server DTOs; no financial policy is reimplemented in the app.

An uncertain submission retains its original payload/key in memory and locks editing. Retrying sends the same request, and the server returns the original order if already committed. A simulated lost committed response is tested. Leaving an uncertain submission prompts for confirmation; an in-flight submission must first complete or be cancelled. Unlocking requires explicit acknowledgement to reconcile history/admin before a different request. There is no offline queue or persistent draft. After process termination during an uncertain request, inspect history before starting another order.

Errors include validation/ProblemDetails messages, network/certificate guidance, timeout, incompatible response, revoked/expired session and unavailable secure storage. Network operations are async and cancellable. Automatic page navigation cancellation is silent; explicit Cancel gives feedback. Android backup is disabled to avoid restoring unusable secure-storage material.

## Tests and evidence
The integration test project links the exact platform-neutral Client and ViewModels source files. This tests real client serialization, authentication, navigation decisions and retries against the SQLite-backed test server without pretending xUnit instantiates MAUI native controls. Platform adapters and XAML require separate build/runtime evidence.

MOB01–MOB08 cover the full client/VM flow and administrator website visibility, registration/ownership, historical prices/debt, restore/origin binding/admin rejection, cancellation/network/expiry, four unsafe origins, exact local certificate pins, lost-response idempotency and lifecycle cancellation feedback. See TEST_LOG.md for actual command/count/runtime results and failed attempts.

