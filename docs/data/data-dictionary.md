# Phase 0 data dictionary

Only foundation tables are implemented. All identifiers are UUID; timestamps are UTC `timestamptz`.

## `identity.organisations`

Tenant root containing `id`, unique `code`, `name`, lifecycle `status`, IANA `default_timezone`, ISO `default_currency`, created/updated timestamps, and PostgreSQL `xmin` concurrency token.

## `identity.users`

Minimal external-identity mapping containing tenant `organisation_id`, `external_subject`, email, display name, status, last login, timestamps, and `xmin`. Subject and email are unique inside a tenant. It contains no password, access token, or provider secret.

## `identity.user_permission_grants`

Development/reference grant projection containing tenant, user, stable permission code, scope, optional scope reference, allow/deny effect, and validity interval. Full roles, groups, assignments, delegation, and policy administration belong to Phase 1.

## `platform.audit_events`

Append-only evidence containing tenant, actor/effective user, action, entity reference, optional project reference, UTC time, outcome/reason, correlation/causation, source, and redacted JSON change summary. Application and database controls reject update/delete.

## `platform.outbox_messages`

Versioned event envelope plus publication status, retry count, next attempt, safe last error, and publication time. Pending records are indexed for dispatcher polling.

## `platform.inbox_messages`

Consumer name plus message ID composite key, receipt/completion time, and outcome. The key makes duplicate delivery idempotent.
