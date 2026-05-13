# Codex Task — Phase 11: Dockerfile + Railway Deployment

> Read `AGENTS.md` first. Phases 0–10 must be complete before starting this.

## Goal

By the end of this phase:
- The backend builds into a minimal Docker image (multi-stage, non-root user, port 8080)
- `railway.json` configures Railway.app deployment from the Dockerfile
- `appsettings.Production.json` sets production defaults; all secrets are injected via environment variables
- The app starts cleanly with `docker compose up` locally

---

## Repo root

`C:\Users\piros\OneDrive\Desktop\Pasukhi\`

---

## Step 1 — Dockerfile

Create at the repo root (next to `Pasukhi.sln`):

### `Dockerfile`

```dockerfile
FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /app

COPY Pasukhi.sln .
COPY src/Pasukhi.API/Pasukhi.API.csproj src/Pasukhi.API/
COPY src/Pasukhi.Application/Pasukhi.Application.csproj src/Pasukhi.Application/
COPY src/Pasukhi.Domain/Pasukhi.Domain.csproj src/Pasukhi.Domain/
COPY src/Pasukhi.Infrastructure/Pasukhi.Infrastructure.csproj src/Pasukhi.Infrastructure/

RUN dotnet restore src/Pasukhi.API/Pasukhi.API.csproj

COPY src/ src/

RUN dotnet publish src/Pasukhi.API/Pasukhi.API.csproj -c Release -o /app/publish --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS runtime
WORKDIR /app

RUN adduser --disabled-password --gecos "" appuser && chown -R appuser /app
USER appuser

COPY --from=build /app/publish .

ENV ASPNETCORE_URLS=http://+:8080
EXPOSE 8080

ENTRYPOINT ["dotnet", "Pasukhi.API.dll"]
```

> The image does NOT run EF migrations automatically. Run them separately before or during deployment. For Railway, add a release command or run them in a startup script.

---

## Step 2 — Railway Config

### `railway.json`

```json
{
  "$schema": "https://railway.com/railway.schema.json",
  "build": {
    "builder": "DOCKERFILE",
    "dockerfilePath": "Dockerfile"
  },
  "deploy": {
    "runtime": "V2",
    "numReplicas": 1,
    "sleepApplication": false,
    "restartPolicyType": "ON_FAILURE",
    "restartPolicyMaxRetries": 10
  }
}
```

---

## Step 3 — Production appsettings

### `src/Pasukhi.API/appsettings.Production.json`

```json
{
  "Serilog": {
    "MinimumLevel": {
      "Default": "Information",
      "Override": {
        "Microsoft.AspNetCore": "Warning",
        "Microsoft.EntityFrameworkCore": "Warning",
        "System": "Warning"
      }
    }
  },
  "Jwt": {
    "Issuer": "https://api.pasukhi.ge",
    "Audience": "https://admin.pasukhi.ge",
    "AccessTokenExpirationMinutes": 15,
    "RefreshTokenExpirationDays": 7
  },
  "Meta": {
    "GraphBaseUrl": "https://graph.facebook.com",
    "GraphApiVersion": "v21.0"
  },
  "AI": {
    "Provider": "Gemini",
    "Model": "gemini-2.0-flash-lite",
    "MaxTokens": 500,
    "Temperature": 0.3,
    "RequestTimeoutSeconds": 30,
    "ReasoningEffort": "low"
  },
  "AllowedHosts": "api.pasukhi.ge"
}
```

All secrets are injected as environment variables. The table below maps config keys to env vars:

| Config key | Environment variable |
|---|---|
| `ConnectionStrings:DefaultConnection` | `ConnectionStrings__DefaultConnection` |
| `Jwt:Secret` | `Jwt__Secret` |
| `RabbitMQ:Host` | `RabbitMQ__Host` |
| `RabbitMQ:Username` | `RabbitMQ__Username` |
| `RabbitMQ:Password` | `RabbitMQ__Password` |
| `AI:ApiKey` | `AI__ApiKey` |
| `Meta:AppSecret` | `Meta__AppSecret` |
| `Meta:AppId` | `Meta__AppId` |

Set these in Railway's service environment variable panel. Never commit real values.

---

## Step 4 — Local Docker Test

Build and run locally to verify the image works:

```bash
# Build
docker build -t pasukhi-api .

# Run with dev env vars (adjust values)
docker run --rm -p 8080:8080 \
  -e ASPNETCORE_ENVIRONMENT=Development \
  -e ConnectionStrings__DefaultConnection="Host=host.docker.internal;Port=55433;Database=pasukhi_dev;Username=postgres;Password=postgres" \
  -e Jwt__Secret="dev-secret-key-at-least-32-chars!!" \
  -e RabbitMQ__Host="host.docker.internal" \
  pasukhi-api
```

Confirm `GET http://localhost:8080/api/health` returns 200.

---

## Step 5 — Migrations in Production

EF migrations must run before the API starts. Options:

**Option A — Railway release command** (recommended):

In Railway project settings, set the release command:

```
dotnet ef database update --project src/Pasukhi.Infrastructure --startup-project src/Pasukhi.API
```

This requires the SDK image. Alternatively, build a migration runner stage in the Dockerfile.

**Option B — Startup migration** (simpler):

In `Program.cs`, before `app.Run()`, add:

```csharp
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<PasukhiDbContext>();
    await db.Database.MigrateAsync();
}
```

> Option B runs migrations on every startup — safe for single-replica deployments, not recommended for multi-replica.

---

## Verification

```bash
# Build succeeds
docker build -t pasukhi-api .

# Image size check
docker image inspect pasukhi-api --format '{{.Size}}' | awk '{print $1/1024/1024 " MB"}'
# Expected: < 250 MB
```

Deploy to Railway:
1. Push the branch to GitHub
2. Connect the Railway project to the repo
3. Set all environment variables
4. Trigger a deploy — confirm health endpoint returns 200

---

## Commit

```bash
git add Dockerfile railway.json src/Pasukhi.API/appsettings.Production.json docs/codex/phase-11.md
git commit -m "feat(11): Dockerfile, Railway config, and production appsettings"
```

---

## What's Next

The core platform is complete. Potential next phases:

- **Phase 12**: WhatsApp Business API — media message sending, read receipts
- **Phase 13**: Conversation tagging and search
- **Phase 14**: Multi-operator support with role-based access to businesses
- **Phase 15**: Webhook delivery retries and dead-letter monitoring UI
