# Pasukhi — პასუხი

> Multi-tenant B2B SaaS platform that automates customer replies across Instagram, Facebook Messenger, and WhatsApp using FAQ/rules-first processing with AI fallback.

Built with **ASP.NET Core 9**, **PostgreSQL**, **RabbitMQ**, and **React + TypeScript**.

---

## What Is This?

Pasukhi (Georgian: "Answer") is a messaging automation platform for small businesses. Instead of manually replying to the same customer questions every day, businesses connect their Instagram, Facebook Messenger, and WhatsApp accounts and let the platform handle repetitive replies automatically — using a FAQ knowledge base, automation rules, and AI as a last resort.

## Architecture

The full architecture blueprint is in [`docs/ARCHITECTURE.md`](docs/ARCHITECTURE.md).

It covers:
- Modular monolith design with clean architecture
- Multi-tenant isolation strategy (day one)
- Unified message model across all three channels
- RabbitMQ async processing pipeline
- FAQ-first / AI-second routing logic
- PostgreSQL schema with EF Core global query filters
- React admin panel structure
- 7-phase implementation order (~30 days)

## Tech Stack

| Layer | Technology |
|-------|-----------|
| Backend API | ASP.NET Core 9 Web API |
| Database | PostgreSQL 16 + Entity Framework Core 9 |
| Async Processing | RabbitMQ 3.13 + MassTransit |
| Frontend | React 18 + TypeScript + Vite |
| State Management | TanStack Query + Zustand |
| UI | Tailwind CSS + shadcn/ui |
| Validation | FluentValidation (BE) + Zod (FE) |
| AI | OpenAI / Anthropic (behind IAiService abstraction) |
| Auth | JWT + HttpOnly refresh token cookies |
| Logging | Serilog → structured logs |
| Deployment | Single Linux VM or Azure App Service |

## Channels Supported

- **Instagram Messaging** (via Meta Graph API)
- **Facebook Messenger** (via Meta Graph API)
- **WhatsApp Business** (via Meta Cloud API)

All three channels normalize into a single `NormalizedInboundMessage` model and route through the same processing pipeline.

## Project Status

🏗️ **Architecture phase** — Blueprint complete, implementation starting.

---

*See [`docs/ARCHITECTURE.md`](docs/ARCHITECTURE.md) for the full design.*
