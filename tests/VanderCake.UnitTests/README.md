# Unit tests

Phase 02 tests cover entity invariants, integer money conversion/limits, locale-independent price validation, stock/date eligibility, explicit order transitions, sale snapshots, payment-term cent remainders/calendar dates and settlement arithmetic/state changes.

These tests exercise domain behavior without HTTP, EF or UI. Operational FEFO allocation and payment application-service transactions remain future work.
