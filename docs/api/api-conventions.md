# API conventions

- Routes begin `/api/v1/` and use plural resource nouns. Explicit commands represent state transitions.
- OpenAPI is emitted at `/openapi/v1.json` and validated in CI.
- Errors use RFC 9457 Problem Details with stable `code`, safe `detail`, `correlationId`, and optional field errors.
- Never expose stack traces, SQL, internal paths, secrets, or protected record existence.
- Mutable resources use ETags or a row-version token; stale writes return `409` with code `concurrency_conflict`.
- Create, submit, import, approve, and callback commands require an idempotency key. The server stores key, principal, tenant, request fingerprint, status, and safe response reference for the documented retention period.
- Date-times use ISO 8601 offsets at boundaries and UTC internally. Money is `{ amount, currency }`.
- Lists have bounded pages, stable sort, and opaque cursors.
