# PLANOVA architecture constitution

This constitution is binding for every implementation change. A conflicting change requires an accepted ADR before code is merged.

## Non-negotiable rules

1. Never check a role name in business logic or UI code. Authorisation uses stable permission codes and evaluated scopes.
2. Deny access by default.
3. Authorise every protected command and query on the server, including list, search, count, export, notification, and background work.
4. Never trust organisation, user, scope, state, price, permission, or approval information submitted by a client.
5. A module owns and writes its own data.
6. Direct cross-module database writes are prohibited.
7. Cross-module events are explicit, immutable, and versioned.
8. Event consumers are idempotent and tolerate duplicate delivery.
9. Material operations produce immutable audit evidence in the same transaction where feasible.
10. Secrets, tokens, credentials, private payloads, and sensitive values are never written to logs or audit payloads.
11. Business transitions use explicit commands; generic state mutation endpoints are prohibited.
12. Applied production migrations are never rewritten. Corrections use forward migrations.
13. Architecture deviations require an ADR with context, decision, consequences, and review conditions.

## Boundary test

Domain projects may depend only on their Contracts and the Shared Kernel. Application projects may depend on their own Domain and Contracts. Infrastructure implements Application ports. API projects expose Application capabilities. The composition root may reference implementations; other modules may not.

The Shared Kernel contains stable technical primitives only—never business entities, workflows, repositories, or module-specific services.
