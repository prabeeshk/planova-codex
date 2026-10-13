# Authorisation model

Authentication answers who the principal is. PLANOVA authorisation answers what the principal may do to a specific record now.

```text
principal + permission + scope + scope reference + validity + conditions
```

Effective access is the union of applicable valid allows minus applicable valid denies. A deny wins. Supported scopes are SELF, ASSIGNED, TEAM, PROJECT, DEPARTMENT, PORTFOLIO, and ORGANISATION. The server resolves tenant, hierarchy, ownership, assignment, and membership; clients never submit effective access.

Every protected handler declares a permission and an `AuthorizationResource`. The evaluator first verifies authentication and exact tenant equality, then loads server-side grants, rejects applicable denies, and finally accepts an applicable allow. Missing grants deny. Record lookups should apply tenant predicates before identifier predicates; callers return `404` instead of `403` when acknowledging existence would leak protected data.

Trust boundaries are the browser/API boundary, OIDC provider callback, database connection, outbox sink, and future integration/webhook boundaries. Navigation filtering is usability only and never replaces endpoint authorisation.
