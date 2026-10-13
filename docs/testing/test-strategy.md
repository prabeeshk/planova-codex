# Test strategy

Unit tests cover primitives and domain rules. Integration tests cover EF mappings, tenant filters, audit append-only behavior, outbox atomic persistence, and inbox deduplication. API tests cover authentication, allow/deny, cross-tenant attempts, validation, safe Problem Details, and health. Architecture tests inspect project references and source to prevent forbidden dependencies and role-name checks. End-to-end smoke tests cover shell landmarks and accessibility-critical markup.

Security suites always include at least two organisations plus allowed and denied principals. PostgreSQL migrations are applied to an empty CI service. Provider-independent tests may use EF InMemory only for fast behavioral feedback; they do not replace PostgreSQL migration validation.
