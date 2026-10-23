# API reference — Phases 02/03

Base URL for the local HTTPS profile: https://localhost:7080.
JSON uses camelCase. Monetary response fields ending Minor are integer BRL cents; product Price is decimal BRL input/output. Unexpected JSON members are rejected. Collection routes return at most 500 rows for the academic demo; pagination is future work.

## Authentication
POST /api/auth/login with email/password -> 200:
- tokenType: Bearer
- accessToken: opaque protected token
- expiresIn: 1800 seconds
- refreshToken: framework field; no refresh endpoint exists, so re-login after expiry.

Use Authorization: Bearer <accessToken> on protected requests. No credentials or token examples containing real values are stored here. Roles are Administrator and Salesperson.
GET /api/auth/me returns user ID/email/roles.
POST /api/auth/logout returns 204 and invalidates existing tokens via the security stamp.

All APIs require HTTPS. Cookies do not authenticate API policies; bearer tokens do not authenticate administrator Razor pages. Invalid/tampered/expired tokens return 401. A token with revoked stamp, disabled identity or stale roles returns 403. Invalid credentials (including account lockout) return a generic 401.

## Customer endpoints
- GET /api/customers — salesperson's portfolio in visit-order/ID order; administrator sees all.
- GET /api/customers/{id} — 200, or 404 for absent/out-of-portfolio records.
- POST /api/customers — 201 + Location.
- PUT /api/customers/{id} — 200; body contains customer + version; stale version -> 409.

Customer body:
```json
{
  "name": "Example bakery",
  "cityId": "<GUID from reference/cities>",
  "visitOrder": 1,
  "address": "Example address",
  "phone": "11900000000",
  "salespersonId": null,
  "isActive": true
}
```
The salesperson's owner ID is derived server-side. An explicit different salespersonId is forbidden. Administrator creation/reassignment requires an active salespersonId. City must exist. Customers are deactivated through update, not deleted.

## Product endpoints
- GET /api/products and GET /api/products/{id} — active catalogue for salesperson, all for administrator.
- POST /api/products — administrator only, 201 + Location.
- PUT /api/products/{id} — administrator only, body product + version; 200 or 409.

Product body fields: code, name, productGroupId, saleUnit, price (decimal), isActive.
A price of 1.005 becomes priceMinor=101 and price=1.01. Price must round to a positive cent value and stay within the documented monetary limit. Product codes are normalized uppercase and unique.

## Orders
- GET /api/orders — authorized orders; newest first.
- GET /api/orders/{id} — 200 or ownership-safe 404.
- POST /api/orders — salesperson only.

```json
{
  "customerId": "<assigned active customer GUID>",
  "paymentTermCode": 10,
  "dispatchDate": "2026-10-22",
  "dispatchHour": 19,
  "submissionKey": "<fresh GUID>",
  "items": [{ "productId": "<active product GUID>", "quantity": 5 }]
}
```

Use an actual future dispatch date. Allowed hours: 12 or 19, interpreted in America/Sao_Paulo. The server determines prices, salesperson, submission timestamp and Submitted state. At least one and at most 100 distinct product lines; quantities 1–1,000,000.

First submission -> 201 + Location; identical retry with same key -> 200 same order. Changed content with reused key -> 409. Reordering lines is canonicalized. Order responses expose requested/fulfilled/unfulfilled quantities and snapshot prices. Fulfillment endpoints do not exist yet.

## Reference data
Authenticated:
- GET /api/reference/states
- GET /api/reference/cities
- GET /api/reference/product-groups
- GET /api/reference/payment-terms

Administrator only: GET /api/reference/salespersons.
Migration seeds two states (SP/MG), three cities and payment terms. Demo initialization adds four product groups/products and salesperson/customer/lot examples.

## Errors
Errors use application/problem+json with status/title and traceId.
400: shape/domain validation; DataAnnotations errors also contain errors by field.
401: missing/invalid authentication.
403: wrong role, inactive identity/profile, revoked claim or forged customer assignment.
404: absent or inaccessible resource.
409: stale version, duplicate code/submission-content mismatch or relational conflict.
Malformed JSON and unexpected fields -> 400, never a stack trace. Unsupported methods may return 405.

Administrator website: /Account/Login, /Admin and POST /Admin/Logout. Forms require real antiforgery tokens; passwords are not shipped in source.

Phase 03 now implements sales/history/debt, stock reads, manual allocation and full-installment payment APIs; see WORKFLOW.md for contracts, examples, idempotency and statuses. Inventory receipts and master-account management remain pending. Balance/history/finance queries are not truncated to the 500-row catalogue/order list cap: they include all records needed for accurate customer debt.
