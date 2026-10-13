# ADR-002: Module data ownership

- Status: Accepted
- Date: 2026-10-13

Each module owns a PostgreSQL schema and its migrations. A module never writes another module's tables. Cross-module identifiers are validated through contracts and reconciled by events. The Phase 0 `identity` and `platform` schemas are independently migrated. Production migration history is append-only and migration files are never edited after deployment.
