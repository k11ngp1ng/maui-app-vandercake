# ADR 002 — Persistent SQLite, Identity and local bearer authentication

Date: 2026-10-21 (America/Sao_Paulo). Status: implemented in Phase 02.

## Decision
Keep the established project boundaries. Domain owns pure business entities and rules; ASP.NET Core Identity users and roles live in Infrastructure and its IdentityDbContext. Salesperson.UserId has a required, unique foreign key to the Identity user. No duplicate domain password/user store is introduced.

Use integer cents for money and integer basis points for percentages. Convert decimal input with MidpointRounding.AwayFromZero; final installment receives the remainder. Domain bounds are 1,000,000 units per line/lot, 100 order lines and 1,000,000,000,000 cents per price/total. These explicit demo limits prevent overflow and uncontrolled requests.

Persist SQLite by default under the current user's LocalApplicationData/VanderCake/vandercake.db, outside OneDrive. An explicit connection string can override this. Apply EF migrations explicitly with dotnet ef database update or the --migrate application command; ordinary startup performs no migration/reset/seed. Migration includes stable SP/MG/city references and immutable codes 10/20 installment definitions.

## Authentication
Use Identity password hashing, five-failure/five-minute account lockout, active-user checks, role policies and security-stamp validation. Administrator pages use an HttpOnly, Secure, SameSite=Lax cookie and Razor antiforgery. Login return URLs must be local.

Native APIs explicitly require Identity's bearer scheme. Built-in ASP.NET Core protected opaque access tokens are authenticated/encrypted using Data Protection, scoped by application/scheme and expire after 30 minutes. These are **not JWTs**; JWT issuer/audience settings do not apply. On each authorized request, additionally verify the current user is active, the security stamp matches and roles still match the database. Stale/revoked claims are forbidden. Logout updates the security stamp, invalidating prior API tokens. No public registration or refresh endpoint is exposed; re-login after expiry. The framework's unused refreshToken response field is not accepted by any route.

Default Data Protection key persistence uses ASP.NET Core's host defaults (Windows user profile/DPAPI in this environment). Keys are not in the repository. The real-process restart check verified an existing token survives an ordinary process restart. A deliberate key deletion invalidates tokens/cookies.

All /api and /Account routes require HTTPS even in Development; login forms are not served over HTTP. Health and informational / remain available over local HTTP. Tests use HTTPS test-host addresses; one local process-check script skips certificate trust validation only for its localhost requests. No application certificate-validation bypass is installed.

## Deployment boundary
This protected-token approach is appropriate only for the local academic demonstration. A production deployment requires a standards-based OpenID Connect/OAuth 2.0 identity solution, properly validated access tokens, secure HTTPS/certificate configuration and durable protected key storage. Do not turn this demo into a custom production token issuer.

## Data authorization and consistency
Resolve salesperson identity from the authenticated user. Scope customers by current assignment; scope orders by original salesperson AND customer's current assignment. Reassignment revokes the former salesperson's access. The new salesperson does not gain authorship of the old submitted order. Administrators can read all through administrator-authorized services.

Customer/product updates require a Version token. EF uses application-managed Guid concurrency tokens; unique keys protect product code and salesperson/submission-key pairs. A repeated order submission key with identical canonical content returns the original order; changed content returns 409. Server snapshots prices and ignores no unknown request fields: unexpected JSON fields are rejected.

Fulfillment, FEFO orchestration and payment application services are deferred. Their domain entities/mappings exist, but no endpoint can consume inventory, confirm fulfillment or settle a receivable yet.

## References
- [Identity API authentication](https://learn.microsoft.com/en-us/aspnet/core/security/authentication/identity-api-authorization?view=aspnetcore-10.0)
- [BearerTokenOptions](https://learn.microsoft.com/en-us/dotnet/api/microsoft.aspnetcore.authentication.bearertoken.bearertokenoptions?view=aspnetcore-10.0)
- [EF design-time DbContext creation](https://learn.microsoft.com/ef/core/miscellaneous/cli/dbcontext-creation)

