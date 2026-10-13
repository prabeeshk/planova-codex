# Phase 2 implementation status

Updated: 2026-10-13

## Prerequisites

- [x] Product, architecture, security, data, API, testing, migration, and Phase 1 material inspected.
- [x] Phase 1 authentication, tenant context, Identity references, scoped grants, audit/outbox, concurrency foundation, Problem Details, and capability navigation confirmed.
- [x] Pre-Phase-2 automated suite passed (25/25).
- [ ] Apply Phase 1 and Phase 2 migrations to empty and upgraded live PostgreSQL databases; no local PostgreSQL/container runtime has yet been verified.

The brief named `docs/PLANOVA_Enterprise_PRD.md`; the repository contains `docs/PLANOVA_PRD.md`, which is treated as the authoritative equivalent.

## Slice checklist

- [x] Slice 1 — project creation and authorised retrieval. Domain, schema, API, accessible create/list/detail UI, Identity reference validation, scoped retrieval, idempotency, audit/outbox, and automated verification implemented.
- [x] Slice 2 — reasoned editing, ownership-transfer checks, optimistic concurrency, append-only lifecycle history, explicit transition matrix, and archival rules.
- [ ] Slice 3 — dated membership records and stable roles are implemented, but Identity grant reconciliation, grant-authority enforcement, cache invalidation, and configurable role templates remain open.
- [ ] Slice 4 — append-only manual health reporting, correction links, deterministic current projection, history API, and overview display are implemented. Health entry UI remains open.
- [ ] Slice 5 — immutable established baseline versions, supersession, snapshot hashes, establishment evidence, date comparison, and manual-progress settings are implemented. Baseline entry UI, settings mutation, broader hardening tests, and live-database migration verification remain open.

## Slice 1 decisions

1. Project creation is authorised against the proposed owning unit using `project.create` with DEPARTMENT or ORGANISATION scope.
2. `project.view` evaluates ORGANISATION, DEPARTMENT, exact PROJECT, or ASSIGNED scope. For Slice 1, ASSIGNED means the designated manager only; membership responsibilities are added in Slice 3.
3. A missing or inaccessible project returns the same safe `not_found` response.
4. Create idempotency binds organisation, actor, and key to a SHA-256 request fingerprint. Same-key/same-payload returns the original project; changed payload returns `idempotency_key_reused`.
5. Projects validates active user and organisation-unit references through a read-only composition adapter; it never writes Identity data.
6. Project creation and platform evidence share a PostgreSQL transaction. The in-memory provider persists the two contexts sequentially and is not evidence of database atomicity.
7. Portfolio association, tags, tasks, budgets, allocations, approvals, and derived progress are absent by design.

## Verification log

- Slice 1: build succeeded with zero warnings; 30/30 tests passed (11 unit, 3 integration, 3 architecture, 13 end-to-end).
- Current branch: build succeeds with zero warnings; 33/33 automated tests pass (11 unit, 4 integration, 3 architecture, 15 end-to-end). The added journey covers lifecycle, membership persistence, health, baseline establishment, invalid transitions, stale updates, and outbox evidence.
- Migration discovery is verified without a database. Applying both migrations to empty and Phase-1 PostgreSQL databases is still unverified because no live PostgreSQL runtime is available in this workspace.

## Lifecycle transition matrix

| From | Allowed targets |
| --- | --- |
| Draft | Proposed, Cancelled |
| Proposed | Draft, Active, Cancelled |
| Active | On Hold, Completed, Cancelled |
| On Hold | Active, Cancelled |
| Completed | Archived |
| Cancelled | Archived |
| Archived | None |

Completion requires an actual end date. Every transition requires a reason and expected concurrency version. Archival uses `project.archive`; all other transitions use `project.edit`. Archived projects reject normal edits and child-record changes.

## Implemented API surface

- `POST /api/v1/projects`, `GET /api/v1/projects`, `GET /api/v1/projects/{id}`
- `PUT /api/v1/projects/{id}` and `POST /api/v1/projects/{id}/transitions`
- `GET|POST /api/v1/projects/{id}/members` and `DELETE /api/v1/projects/{id}/members/{memberId}`
- `GET|POST /api/v1/projects/{id}/health`
- `GET|POST /api/v1/projects/{id}/baselines` and `POST /api/v1/projects/{id}/baselines/{baselineId}/establish`
- `GET /api/v1/projects/{id}/settings`

All operations retain tenant query filters, safe inaccessible-record behaviour, server-side permission checks, audit records, and versioned outbox event envelopes. Baseline establishment is a direct `project.baseline` decision and is not described as workflow approval.

## Remaining work before Phase 2 completion

1. Define configurable project-role templates and reconcile their scoped permission assignments through an Identity-owned public contract without exceeding the actor's grant authority.
2. Make membership effective dates/revocation participate in assigned-scope resolution and access-cache invalidation; prove failure recovery or idempotent reconciliation.
3. Add member, health, and baseline authoring UI with capability-derived actions and accessible conflict/success states.
4. Add settings mutation, richer list filters/sort/pagination, and baseline comparison beyond current date variance.
5. Run empty-database and Phase-1-upgrade migrations against PostgreSQL and add cross-tenant, grant escalation, expiry, archival-history, future-health, baseline immutability, and accessibility automation.
