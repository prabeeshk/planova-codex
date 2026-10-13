# ADR-003: Authentication and authorisation

- Status: Accepted
- Date: 2026-10-13

Corporate authentication uses OIDC authorization code flow with PKCE, secure server cookies, provider-managed MFA, and no token persistence (`SaveTokens=false`). Entra ID is the first target. PLANOVA maps the validated external subject to an internal user and organisation. Effective authorisation is resolved from current server-side grants, scopes, validity, membership, and denies; external claims do not grant business capability by themselves.

A header-based development scheme exists solely for deterministic local/testing identities. Startup rejects it outside the Development environment. No custom passwords are stored. CSRF protection is enabled for cookie-authenticated interactive requests.

Production still requires decisions for tenant-to-Entra mapping, claims transformation/JIT provisioning, logout/revocation, and privileged recent-authentication policy before go-live.
