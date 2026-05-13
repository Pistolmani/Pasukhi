# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Commands

### Backend (ASP.NET Core 9)
```bash
dotnet build Pasukhi.sln
dotnet run --project src/Pasukhi.API/Pasukhi.API.csproj   # requires PostgreSQL + RabbitMQ
docker-compose up -d                                        # start dependencies
```

### Tests
```bash
dotnet test                                                          # all tests
dotnet test tests/Pasukhi.UnitTests/Pasukhi.UnitTests.csproj         # unit only
dotnet test tests/Pasukhi.IntegrationTests/Pasukhi.IntegrationTests.csproj
dotnet test --filter "FullyQualifiedName~FaqMatcherTests"            # single class
dotnet test --filter "FullyQualifiedName~FaqMatcherTests.MethodName" # single method
```

### Frontend (pasukhi-admin/)
```bash
npm install && npm run dev    # http://localhost:5173
npm run build
npm run lint
```

### Docker
```bash
docker-compose up             # PostgreSQL 16 (port 55433) + RabbitMQ 3.13 (5672/15672)
docker build -t pasukhi-api . # multi-stage, runs as non-root on port 8080
```

## Architecture

Multi-tenant SaaS that automates replies on Instagram, Facebook Messenger, and WhatsApp via an FAQ → rules → AI fallback pipeline.

### Project layers (clean architecture)
```
Pasukhi.Domain          → Entities, Enums — no external dependencies
Pasukhi.Application     → Service interfaces, DTOs, FluentValidation validators, MassTransit message contracts
Pasukhi.Infrastructure  → EF Core + PostgreSQL, MassTransit consumers, Meta Graph API channel adapters, service implementations
Pasukhi.API             → ASP.NET Core controllers, middleware, Program.cs DI setup
```

### Multi-tenancy
Every entity inherits `TenantEntity` (`BusinessId`, `CreatedAt`, `UpdatedAt`). EF Core global query filters on `BusinessId` enforce isolation — never bypass them. `ITenantContext` (mutable) / `ITenantProvider` (immutable) are populated from JWT claims on HTTP requests and from message headers in queue consumers. The `TenantContextFilter<T>` MassTransit filter sets tenant automatically before `Consume()` runs.

### Inbound message flow
```
Meta Webhook → WebhookController (verify sig → publish to queue → return 200)
    → RabbitMQ → InboundMessageConsumer
        1. Persist message
        2. FaqMatcher   (keyword + optional semantic match)
        3. RuleMatcher  (priority-ordered automation rules)
        4. AiService    (Gemini default, OpenAI alternative — set via AI:Provider config)
        5. EscalationService (if confidence < threshold or AI fails)
    → OutboundMessageConsumer → Instagram / Messenger / WhatsApp channel provider
```

The webhook handler must stay fast — no DB writes, no AI calls inline. All heavy work goes through the queue.

### Key patterns
- **No MediatR** — services are injected directly into controllers and consumers.
- **AI provider selection** — `builder.Configuration["AI:Provider"]` at startup picks the `IAiService` implementation.
- **Idempotency** — unique index on `ExternalMessageId` prevents duplicate processing.
- **Retry logic** — MassTransit exponential backoff: 3 retries, 2–30s delays.

### Authentication
JWT (15 min) + HttpOnly refresh cookie (7 days). ASP.NET Core Identity for admin users. `BusinessId` is stored in JWT claims and is the source of truth for tenant context.

### Database
PostgreSQL via EF Core. Migrations live in `src/Pasukhi.Infrastructure/Data/Migrations/`. Key unique indexes: `ExternalMessageId`, `ExternalAccountId + ChannelType`, `BusinessId + Key` (settings).

### Frontend
React 19 + TypeScript + Vite. TanStack Query 5 for server state, Zustand for client state, React Hook Form + Zod for validation, Axios with JWT interceptor.

## Configuration

`appsettings.Development.json` — local dev defaults (PostgreSQL at `127.0.0.1:55433`, CORS for `localhost:5173`, empty AI/Meta keys).
`appsettings.Production.json` — production log levels (Information), domain URLs, JWT expiry (15min / 7d), AI config, allowed hosts.

Required environment variables for production (Railway / Docker):
- `ConnectionStrings__DefaultConnection` — PostgreSQL connection string
- `Jwt__Secret` (32+ chars minimum)
- `RabbitMQ__Host`, `RabbitMQ__Username`, `RabbitMQ__Password`
- `AI__ApiKey` — Gemini or OpenAI API key (set `AI__Provider` to toggle)
- `Meta__AppSecret`, `Meta__AppId` — Facebook app credentials
- `ASPNETCORE_ENVIRONMENT=Production` in Railway / Docker runtime

## Database

Run migrations:
```bash
dotnet ef database update --project src/Pasukhi.Infrastructure --startup-project src/Pasukhi.API
```

In production (Railway), set a release command in Railway project settings to run migrations before the API starts:
```
dotnet ef database update --project src/Pasukhi.Infrastructure --startup-project src/Pasukhi.API
```

Or use startup migration in `Program.cs` (not recommended for multi-replica deployments).

## Deployment (Railway)

The Dockerfile is multi-stage, builds the Release binary, runs as non-root on port 8080.
- Build: `docker build -t pasukhi-api .`
- Test: `docker run --rm -p 8080:8080 -e ASPNETCORE_ENVIRONMENT=Development ... pasukhi-api`
- `railway.json` configures Railway to use the Dockerfile with V2 runtime, 1 replica, ON_FAILURE restart.

See `docs/codex/phase-11.md` for full deployment checklist.

## Development Workflow

Phases 0–11 define feature increments. Each phase:
1. Read the phase codex file: `docs/codex/phase-N.md`
2. Implement backend entities, services, controllers
3. Implement frontend pages / forms
4. Run `dotnet build` and `tsc --noEmit` to verify compilation
5. Commit: `git commit -m "feat(N): description"`

See `AGENTS.md` for critical architectural rules (tenant isolation, webhook speed, FAQ→Rules→AI order, etc.).
