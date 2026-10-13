# PLANOVA

PLANOVA is a modular enterprise platform for project, portfolio, resource, financial, workforce, and governance management. This repository currently implements **Phase 0 only**: architecture, security, persistence, observability, application-shell, and test foundations.

Read [`docs/PLANOVA_PRD.md`](docs/PLANOVA_PRD.md) and the architecture constitution before changing code.

## Quick start

```bash
cp .env.example .env
# Replace the placeholder in .env with a local-only password.
docker compose up --build
curl http://localhost:8080/health/ready
curl -H 'X-Planova-Dev-User: admin-a' http://localhost:8080/api/v1/foundation/context
```

The development identity header is accepted only when `ASPNETCORE_ENVIRONMENT=Development` and `DevelopmentAuth:Enabled=true`. It cannot be enabled in production.

For direct `dotnet run`, provide `ConnectionStrings__Planova` through an environment variable or .NET user-secrets. Never add connection credentials to an appsettings file.
