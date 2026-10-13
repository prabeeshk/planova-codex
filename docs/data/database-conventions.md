# Database conventions

- PostgreSQL is authoritative. Each module owns a schema and migration history table.
- Identifiers are UUIDs; application-generated identifiers use UUIDv7.
- Tenant records contain `organisation_id`; common access indexes start with it.
- UTC instants use `timestamptz`; business dates use `date`; configured zones use IANA names.
- Money uses `numeric(19,4)` and ISO 4217 `char(3)` currency.
- Mutable aggregate roots have created/updated actor/time metadata and optimistic concurrency. PostgreSQL `xmin` is the initial row-version mechanism.
- Unique constraints for tenant data include `organisation_id`.
- Soft deletion is added only when the owning module's requirements call for it.
- JSONB is limited to versioned payloads, redacted audit summaries, and provider metadata; query-critical data remains relational.
- High-volume lists use deterministic order plus a unique tiebreaker and cursor pagination.
- Development/test fixtures are environment-gated; production seed identities are forbidden.

Migration names use `yyyyMMddHHmm_Description`. Generate forward-only migrations in the owning Infrastructure project, inspect SQL, test on an empty database and an upgrade snapshot, then deploy before dependent code when compatibility requires it.
