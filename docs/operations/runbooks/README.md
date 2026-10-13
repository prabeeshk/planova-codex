# Operations runbooks

## Required runbooks before production

- deployment, forward migration, and rollback/forward-fix;
- OIDC outage, signing-key rollover, and access revocation;
- database failover, backup restore, and RPO/RTO verification;
- outbox backlog and dead-letter investigation;
- audit integrity and privileged-access review;
- secret rotation and suspected disclosure;
- readiness failure and degraded dependency response;
- incident communication, evidence preservation, and post-incident review.

## Initial diagnostic sequence

1. Capture the correlation ID without collecting request bodies or credentials.
2. Check `/health/live` and `/health/ready` from the trusted operations network.
3. Inspect structured logs by correlation ID and service version.
4. Check PostgreSQL connectivity, migration state, outbox pending age, and dead-letter count.
5. Apply the documented disable switch or forward fix; never edit audit evidence or applied migrations.
