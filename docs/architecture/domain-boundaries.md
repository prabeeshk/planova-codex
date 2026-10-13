# Domain boundaries

PLANOVA is one modular monolith. Identity owns organisations, users, groups, roles, grants, delegations, and policies. Platform Infrastructure owns audit evidence, outbox delivery, inbox deduplication, correlation, health, and operational adapters. Future modules own the records listed in the PRD and must not be implemented during Phase 0.

Cross-module interaction uses one of:

- a public Contracts assembly for stable identifiers and DTOs;
- an Application query port for an immediate invariant;
- a versioned domain event through the transactional outbox.

Foreign identifiers from another module are references, not permission to update that module's schema. Governance queries authoritative module records and never creates operational copies.
