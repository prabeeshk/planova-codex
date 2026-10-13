# ADR-005: Audit architecture

- Status: Accepted
- Date: 2026-10-13

Audit evidence is distinct from user-facing activity and application logs. It records tenant, actor, effective user, action, entity, UTC time, outcome/reason, correlation/causation, source, and a deliberately redacted JSON change summary. Application code rejects updates/deletes and PostgreSQL adds an update/delete blocking trigger. Audit creation joins the material operation's transaction where feasible.

Tokens, secrets, files, unrestricted bodies, and sensitive personal content are forbidden. Retention, legal hold, tamper-evident external storage, and operator separation remain production deployment decisions.
