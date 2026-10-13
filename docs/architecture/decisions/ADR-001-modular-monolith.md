# ADR-001: Modular monolith

- Status: Accepted
- Date: 2026-10-13

## Context

PLANOVA needs strong consistency, uniform security, and clear domain ownership without evidence that independently deployed services are needed.

## Decision

Build one deployable ASP.NET Core application partitioned into business modules. Enforce dependencies through projects and architecture tests. Extract a service only after measured scaling, reliability, deployment, or ownership pressure and a new ADR.

## Consequences

Deployment and transactions remain simple. Boundaries require continuous enforcement because they are not network boundaries.
