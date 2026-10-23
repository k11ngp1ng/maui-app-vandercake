# Known limitations and delivery risks

Updated2026-10-22. No known failing automated test or remaining compiler error after Phase06 fixes.

- P0 auxiliary requirements incomplete: product-group deactivation; correcting an unconsumed lot. Basic group naming and stock receipt/inspection work. These gaps are not silently reclassified as completed.
- Windows native main workflow and web visual flow have actual prior-phase evidence. Phase06 reran all tests/builds, real HTTPS workflows/routes and restart checks. Android interactive login/order/touch flow and physical-device connectivity remain NOT RUN; only Android build/install/startup smoke is proven.
- Human credentials/trusted HTTPS, dedicated delivery DB and combined presentation rehearsal still need completion. No SDK/build prerequisite was missing in the reviewed environment. No device was attached during Phase06 readiness check.
- SQLite is a single-server academic design: process-local gate, one writer, bounded busy waits and optimistic versions. No sustained load, horizontal deployment, power loss or interrupted migration recovery test. Allocation/payment fault rollback and competing requests are tested.
- Schema upgrade uses SQLite table rebuild/nontransactional foreign-key PRAGMA; stop app and make a consistent backup before maintenance. Normal startup never migrates/resets.
- Lists can cap at500 without paging. No full accessibility/screen-reader/keyboard-only or large-data performance acceptance.
- Native uncertain submissions retain original payload/key in memory, not across process death. Check server history before creating a new request after a crash. No offline queue.
- Full-installment settlement only. No partial payments, credits/refunds/reversals, automatic backorders, fractional units or physical-delivery confirmation.
- Demo penalty5%, daily simple interest2%, daily early discount1% are configurable academic example rates, snapshotted into receivables. Quotes change with dates; principal and current payable are distinct.
- Repeat seed preserves existing credentials and lots; it never resets passwords, replenishes consumed stock or advances old expiry/dispatch dates. Use a new explicitly selected dedicated DB for a fresh fixture. Preserve request manifests after uncertain outcomes; stale uncommitted requests/quotes require deliberate review.
- Development opaque bearer tokens are not a production identity platform. Standards-based identity, HTTPS/key operations, Release packaging/signing, distribution and production deployment remain P1.
- No production secrets, push, publication or deployment. Generated helper credentials live temporarily in ignored metadata and must be removed by Stop-AdminReview after review.

See BACKLOG for statuses and REQUIREMENTS_COVERAGE for acceptance evidence.

Source delivery: repository contents remain untracked/no initial commit; arrange a local checkpoint/backup before submission. No push was performed.

