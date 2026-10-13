# ADR-004: Domain events and transactional outbox

- Status: Accepted
- Date: 2026-10-13

Material changes and their outbox rows commit atomically in PostgreSQL. Envelopes contain message and organisation IDs, event name/version, aggregate identity, UTC occurrence time, correlation/causation IDs, and a versioned JSON payload. A background dispatcher publishes pending records through an adapter, applies bounded exponential retry, and dead-letters after ten failures.

Consumers use `(consumer, message_id)` inbox uniqueness and execute their handler with the inbox write in one unit of work. Phase 0 uses a logging sink; no external broker is introduced.
