# Phase 1 implementation status

Updated: 2026-10-13

## Baseline verification

- [x] Required product, architecture, security, data, API, and testing documents read.
- [x] Phase 0 solution builds.
- [x] Phase 0 automated tests pass (19/19 before Phase 1 changes).
- [x] Authentication, tenant context, capability authorisation, Problem Details, audit, outbox, and module-boundary foundations are present.
- [ ] Apply migrations to a live empty and upgraded PostgreSQL database (local PostgreSQL/container runtime is not currently available).
- [ ] Re-run startup and readiness checks after Phase 1 migrations.

## Implementation plan

- [ ] Slice 1 — organisation settings, organisation-unit hierarchy, tenant-safe APIs/UI, atomic audit/outbox, cycle and cross-tenant tests.
- [ ] Slice 2 — enterprise-linked users, lifecycle commands, dated unit affiliation, groups and memberships.
- [ ] Slice 3 — seeded stable permission catalogue, configurable roles, grants, supported scopes, grant validation.
- [ ] Slice 4 — user/group assignments, effective-access explanation, scope intersection, cache invalidation, idempotency.
- [ ] Slice 5 — bounded delegations, expiry/revocation, emergency-admin protection, administration hardening.
- [ ] Documentation, OpenAPI notes, operations guidance, full build/test/migration/security checks.

## Recorded assumptions

1. Organisation creation is a deployment/platform-authority concern. Organisation administrators can update only their server-resolved organisation.
2. Department scope covers the referenced organisation unit and its descendants. Organisation scope has no reference.
3. Future-owned TEAM, PROJECT, and PORTFOLIO scopes are catalogue contracts only and fail closed until their resolvers exist. ASSIGNED is accepted only where an Identity resource supplies a server-resolved assignee.
4. Role-grant scope and assignment scope are intersected; neither can broaden the other.
5. Unsupported grant conditions are rejected. Recent-authentication enforcement remains an integration hook until the enterprise provider claim policy is configured.
6. Normal grantors cannot grant a permission/scope they do not effectively hold. A designated emergency administrator is the controlled bootstrap authority and the last active designation is protected.

## Progress notes

- Identity mutations and platform audit/outbox evidence now share one PostgreSQL transaction through the Phase 1 transaction coordinator.
- Existing applied migrations will not be modified; Phase 1 uses forward migrations only.
- 2026-10-13 continuation verification: the solution builds and all 25 automated tests pass (9 unit, 3 integration, 3 architecture, 10 end-to-end).
- Phase 1 domain entities, forward migrations, administration services/endpoints, effective-access evaluation, delegations, and read-oriented administration pages are present. The slice checklist remains open pending live PostgreSQL migration/startup verification and broader acceptance coverage.
- Administration end-to-end coverage now verifies capability denial and hierarchy-cycle rejection. The shared Identity/platform transaction coordinator is restricted to the configured PostgreSQL provider; the in-memory test harness persists the two contexts sequentially.
