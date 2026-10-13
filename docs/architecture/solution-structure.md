# Solution structure

```text
src/
  Planova.Api/                 Composition root and HTTP host
  Planova.Web/                 Blazor shell and design tokens
  Planova.SharedKernel/        Stable primitives and ports
  Planova.Infrastructure/      Platform audit/outbox/inbox and adapters
  Modules/Identity/
    Planova.Identity.Domain/
    Planova.Identity.Application/
    Planova.Identity.Infrastructure/
    Planova.Identity.Api/
    Planova.Identity.Contracts/
tests/
  Planova.UnitTests/
  Planova.IntegrationTests/
  Planova.ArchitectureTests/
  Planova.EndToEndTests/
```

Future modules repeat the Domain/Application/Infrastructure/Api/Contracts pattern when their phase begins. Empty speculative projects are not created. `Planova.Api` is the sole executable and hosts both versioned APIs and the server-rendered Blazor shell.
