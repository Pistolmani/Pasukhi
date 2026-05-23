# ADR-001: Architecture Evaluation — Pasukhi Backend

**Status:** Proposed  
**Date:** 2026-05-21  
**Deciders:** Engineering team  

---

## Context

Pasukhi is a multi-tenant SaaS that automates replies on Instagram, Facebook Messenger, and WhatsApp via an FAQ → Rules → AI fallback pipeline. The backend is ASP.NET Core 9 (clean architecture), deployed as a single Railway replica with PostgreSQL. This document evaluates the current architecture across four areas where meaningful trade-offs exist and where future decisions will compound: the messaging backbone, the FAQ matching strategy, the `InboundMessageConsumer` design, and the deployment/scaling posture.

The codebase is currently at Phase 11 (deployment complete). The inline code comments note that `System.Threading.Channels` "replaces MassTransit RabbitMQ consumer for in-process messaging" — meaning the original design used an external broker that was subsequently removed. This is the most significant architectural choice in the codebase and deserves explicit evaluation.

---

## Decision Area 1: Messaging Backbone

### Current state — in-process `Channel<T>`

Webhook receives a Meta payload → verifies signature → writes an `InboundMessageEvent` to a bounded `Channel<InboundMessageEvent>` (capacity 512, `FullMode = Wait`) → returns 200 immediately. A `BackgroundService` drains the channel and calls `InboundMessageConsumer.ProcessAsync`. An equivalent channel and service pair handles outbound delivery.

### Options Considered

#### Option A: In-process Channel (current)

| Dimension | Assessment |
|-----------|------------|
| Complexity | Low — no external broker, no MassTransit config |
| Durability | None — channel is in-memory; restart drops all queued events |
| Horizontal scaling | Blocked — each process has its own channel; two replicas would each process a subset of events with no coordination |
| Backpressure on webhook | Real risk — if AI calls slow the consumer, the 512-slot channel fills up and `WriteAsync` blocks, delaying Meta webhook responses. Meta will retry after ~20s, flooding the queue further |
| Retry | 3 retries with 2/5/10s delays, then drop — no dead-letter queue |
| Ops overhead | Zero |

**Pros:** Zero infrastructure, trivially debuggable, fast for low-volume tenants.  
**Cons:** Messages lost on deploy/crash, can't run more than one replica, AI latency spills back onto the HTTP thread.

#### Option B: RabbitMQ + MassTransit (removed original)

| Dimension | Assessment |
|-----------|------------|
| Complexity | Medium — docker-compose already includes RabbitMQ; MassTransit config is well-known |
| Durability | High — messages survive restarts; configurable dead-letter exchange |
| Horizontal scaling | Supported — competing consumers share the queue |
| Backpressure on webhook | Decoupled — publish to exchange is fast; consumer slowness doesn't affect webhook latency |
| Retry | Exponential backoff + dead-letter exchange built-in |
| Ops overhead | RabbitMQ management UI, connection strings, Railway add-on |

**Pros:** Durable, scalable, decoupled latency.  
**Cons:** External dependency, more moving parts, added Railway cost (~$5–10/mo for a managed instance or a second Railway service).

#### Option C: PostgreSQL-backed outbox (transactional outbox pattern)

| Dimension | Assessment |
|-----------|------------|
| Complexity | Medium — requires an outbox table, a background poller, and careful SaveChanges ordering |
| Durability | High — messages are durable the moment the inbound transaction commits |
| Horizontal scaling | Supported with row-level locking (e.g., `SELECT ... FOR UPDATE SKIP LOCKED`) |
| Backpressure on webhook | Decoupled |
| Retry | Retry counter + scheduled-at column; no extra infrastructure |
| Ops overhead | None beyond the existing PostgreSQL instance |

**Pros:** No new infrastructure, atomic with the inbound DB write (message is never lost even if the process crashes mid-flight), scales horizontally with `SKIP LOCKED`.  
**Cons:** Higher implementation effort than the current approach; polling adds slight latency (~1–2s typical); requires careful index design on the outbox table.

### Trade-off Analysis

For the current scale (single replica, Railway hobby tier), the in-process channel is acceptable with one important caveat: **bounded `Wait` mode puts AI latency directly on the webhook response path**. If Gemini takes 5s and 100 messages arrive in a burst, the channel fills, `WriteAsync` blocks, and Meta's webhook call times out. Meta then retries, compounding the load.

The minimum safe change is switching `FullMode` from `Wait` to `DropOldest` or unbounding the channel, paired with monitoring. Beyond that, the right escalation path is the transactional outbox (Option C) rather than re-introducing RabbitMQ, because it adds durability without a new infrastructure dependency — which matters given the current single-developer, early-stage context.

### Recommendation

**Short term (now):** Switch the inbound channel to `BoundedChannelFullMode.DropOldest` and log a warning when a drop occurs. This prevents webhook starvation. The idempotency check means Meta retries will self-heal.

**Medium term (when adding a second replica or SLA):** Implement the transactional outbox pattern on top of PostgreSQL. Re-evaluate RabbitMQ only if cross-service messaging or fan-out patterns emerge.

### Consequences

- Dropping to `DropOldest` means a burst can lose messages — acceptable given Meta's retry behaviour and the idempotency guard.
- The outbox path requires a migration (outbox table) and a new `BackgroundService` poller.
- Reverting to RabbitMQ would require re-adding the docker-compose service, a Railway add-on, and MassTransit registration.

---

## Decision Area 2: FAQ Matching Strategy

### Current state — lexical scoring

`FaqMatcher` applies three scoring strategies in order: exact/substring match (score 1.0 / 0.92), keyword CSV match (capped at 0.90), token-overlap Jaccard approximation (capped at 0.85). `TextNormalizer` lowercases and strips punctuation. Default threshold is 0.85.

### Options Considered

#### Option A: Lexical matching (current)

| Dimension | Assessment |
|-----------|------------|
| Latency | ~1ms — pure in-process string ops |
| Match quality | Good for exact/close paraphrases; fails on semantic paraphrases ("what time do you close" ≠ "what are your hours") |
| Maintainability | Simple, no external dependencies |
| Multilingual | Works for any script, no special handling needed |

**Pros:** Fast, zero cost, no external dependency, works offline.  
**Cons:** Misses synonym and semantic paraphrases; requires operators to manually add keywords for every variation.

#### Option B: Embeddings + vector similarity (pgvector)

| Dimension | Assessment |
|-----------|------------|
| Latency | 30–100ms per message (embedding API call) + vector scan |
| Match quality | Handles semantic paraphrases, multilingual queries, typos |
| Maintainability | Requires embedding model selection, FAQ re-indexing on change, pgvector extension |
| Cost | ~$0.0001/message for text-embedding-3-small; ~$3/month at 100 msgs/day |

**Pros:** Dramatically better match quality; handles multilingual tenants naturally.  
**Cons:** External API dependency on critical path; FAQ table must be re-embedded on every change; adds pgvector migration; embedding latency adds to per-message processing time.

#### Option C: Hybrid — lexical first, semantic fallback

Run the current lexical scorer first. If confidence < threshold but ≥ a lower bound (e.g., 0.5), fall through to a vector search before proceeding to rules/AI. Cache embeddings in a `FaqEmbedding` table.

| Dimension | Assessment |
|-----------|------------|
| Latency | Same as A for exact matches; A + embedding latency for ambiguous messages |
| Match quality | Best of both worlds |
| Complexity | Medium — needs embedding cache, background re-index job |

### Trade-off Analysis

The current lexical approach is appropriate for an MVP. The real cost shows up when tenants have multi-language audiences (very common for Instagram/WhatsApp in the Caucasus region) or when FAQ questions are phrased differently from how customers write. At moderate scale, lexical matching will produce a high AI-fallback rate and high token spend — which directly affects the per-tenant margin.

The hybrid approach is the pragmatic path: it adds semantic matching only where lexical scoring fails, keeps the hot path fast, and avoids embedding latency on every message.

### Recommendation

**Short term:** Keep lexical matching. Add a metric counter for "FAQ miss → AI fallback" rate per tenant; this will surface the problem when it's real.

**Medium term:** Implement the hybrid approach using pgvector (already available as a PostgreSQL extension) and `text-embedding-3-small`. Store embeddings in a `FaqEmbedding` table keyed by `FaqItemId + model version`. Re-embed on FAQ save via a background job.

### Consequences

- Keeping lexical matching means operators must maintain keyword lists to improve match rates.
- Semantic matching increases per-message latency on the fallback path but can reduce AI token spend significantly if FAQ hit rate improves.
- pgvector requires a migration and a new Railway PostgreSQL extension toggle.

---

## Decision Area 3: InboundMessageConsumer Design

### Current state — monolithic consumer

`InboundMessageConsumer.ProcessAsync` is ~400 lines handling: idempotency check, conversation get-or-create, message insert, metric increment, working-hours check, FAQ matching, rule matching, AI call with safety check, escalation creation, outbound message creation, and a second metric increment. It issues 2–4 `SaveChangesAsync` calls per message.

### Issues Identified

**Multiple `SaveChangesAsync` calls without a saga.** If the process crashes after the inbound message is saved but before the outbound message is saved, the message is persisted but no reply is ever sent. The current retry logic in `InboundMessageBackgroundService` will re-run `ProcessAsync`, but because the inbound idempotency check passes on the second run (the message is already saved), the consumer returns early — meaning the outbound reply is permanently skipped.

**Metric double-initialisation.** `GetOrCreateMetricAsync` is called independently in the inbound persist block and again in `TryCreateOutboundAutoReplyAsync`, both within the same `DbContext` scope. If both calls run before a `SaveChangesAsync`, EF tracks two new `DailyMetric` entities for the same `(BusinessId, Date, ChannelType)`, leading to a unique-constraint violation.

**Match count writes inside read operations.** `FaqMatcher.FindBestMatchAsync` and `RuleMatcher.FindMatchesAsync` both call `SaveChangesAsync` to increment `MatchCount`. This writes inside what is logically a read, adds a round-trip, and means the `DbContext` tracks changes from both the matcher and the consumer simultaneously — an implicit coupling that is easy to break.

**Bounded `ChannelWriter` injected into WebhookController.** The controller directly holds a `ChannelWriter<InboundMessageEvent>`, which means the controller is aware of the transport mechanism. If the transport changes (e.g., to an outbox), the controller must change too.

### Options Considered

#### Option A: Keep current monolith

Simple, works today. Technical debt is manageable at current scale.

#### Option B: Extract match-count updates to a fire-and-forget background batch

Move `MatchCount` increments out of the matching hot path into a periodic batch update (e.g., in-memory counter, flushed every minute). Eliminates the extra `SaveChangesAsync` per match event.

#### Option C: Split consumer into a pipeline of focused steps with a single SaveChanges boundary

Refactor `ProcessAsync` into clearly scoped phases that each return a result object. Collect all DB changes in memory, then call `SaveChangesAsync` exactly once at the end of the processing pipeline. This eliminates the multi-save gap and the metric double-init bug.

### Recommendation

**Fix the metric double-init bug now** — it will produce errors in production under any concurrent load. The fix is to check `_db.ChangeTracker` for a tracked `DailyMetric` before calling `FirstOrDefaultAsync`, or to consolidate metric upserts into a single helper called once.

**Decouple match-count writes** by removing `SaveChangesAsync` from `FaqMatcher` and `RuleMatcher`. Instead, return the matched entity and let the consumer accumulate the `MatchCount` increment as part of its own single save.

**Extract the transport interface.** The `WebhookController` should depend on an `IMessageEnqueuer` interface rather than `ChannelWriter<T>` directly. This makes the transport swappable without touching the controller.

### Consequences

- Single `SaveChangesAsync` per message requires revisiting the try/catch for unique-constraint races (the current pattern of detaching all entries and returning still applies).
- Removing `SaveChangesAsync` from matchers is a minor refactor but eliminates a class of bugs.
- The pipeline split (Option C) is a larger refactor best deferred until the outbox pattern is implemented, since both changes affect save-boundary semantics.

---

## Decision Area 4: Deployment and Scaling Posture

### Current state

Single Railway replica, `ON_FAILURE` restart, EF migrations run at startup (`db.Database.MigrateAsync()`). `railway.json` sets `numReplicas: 1`.

### Issues Identified

**Startup migrations are unsafe for multiple replicas.** `MigrateAsync()` in `Program.cs` is called by every instance at startup. Two replicas starting simultaneously will race on migrations, which can corrupt the migration history table. The CLAUDE.md notes this risk and recommends a Railway release command instead.

**No health check timeout.** The `/api/health` endpoint returns immediately with no dependency checks (no DB ping). Railway's restart policy uses this; a stale DB connection won't trigger a restart.

**Hardcoded IANA→Windows timezone map.** `InboundMessageConsumer` contains a dictionary of 7 entries. Any tenant with a timezone outside this list (e.g., `America/Chicago`, `Asia/Istanbul`) will silently fall back to UTC for working-hours evaluation.

### Recommendations

Replace `db.Database.MigrateAsync()` in `Program.cs` with a Railway release command (`dotnet ef database update ...`), as the CLAUDE.md already suggests. This removes the startup-migration race condition today, before a second replica is ever added.

Expand the health endpoint to include a lightweight DB ping (`SELECT 1`):

```csharp
app.MapGet("/api/health", async (PasukhiDbContext db) => {
    await db.Database.ExecuteSqlRawAsync("SELECT 1");
    return Results.Ok(new { status = "healthy", timestamp = DateTime.UtcNow });
});
```

Replace the hardcoded timezone dictionary with `TimeZoneConverter` (NuGet: `TimeZoneConverter`) which ships a complete IANA↔Windows mapping and is updated with each Windows/IANA release.

---

## Summary of Action Items

| Priority | Item | Effort |
|----------|------|--------|
| **Now** | Switch inbound channel to `DropOldest` and log drops | 5 min |
| **Now** | Fix metric double-init bug in `InboundMessageConsumer` | 30 min |
| **Now** | Remove `SaveChangesAsync` from `FaqMatcher` and `RuleMatcher` | 30 min |
| **Now** | Replace startup `MigrateAsync` with Railway release command | 15 min |
| **Now** | Add `TimeZoneConverter` package; remove hardcoded timezone map | 20 min |
| **Soon** | Extract `IMessageEnqueuer` interface over `ChannelWriter` | 1h |
| **Medium** | Implement transactional outbox pattern (replaces in-process channel) | 2–3 days |
| **Medium** | Add FAQ "miss → AI fallback" metric counter per tenant | 2h |
| **Later** | Hybrid lexical + pgvector FAQ matching | 3–5 days |
| **Later** | Single-save pipeline refactor of `InboundMessageConsumer` | 1 day |

---

## Decision Log

| # | Decision | Rationale |
|---|----------|-----------|
| 1 | Keep in-process channel short-term; switch to `DropOldest` | Avoids webhook starvation; Meta retries self-heal drops |
| 2 | Target transactional outbox over RabbitMQ for durability | Eliminates infrastructure dependency; re-evaluates if fan-out needed |
| 3 | Keep lexical FAQ matching; add miss-rate metric | Defer semantic matching until miss rate is empirically high |
| 4 | Fix metric double-init and match-count write coupling now | Reproducible bugs under real load; low-effort fix |
| 5 | Move migrations to release command | Multi-replica safety before it becomes urgent |
