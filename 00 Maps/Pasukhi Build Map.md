---
tags:
  - map
  - pasukhi
  - build
---

# Pasukhi Build Map

Use this note to track the Pasukhi build phases inside Obsidian.

## Source

- Project repo: `C:\Users\piros\OneDrive\Desktop\Pasukhi`
- Phase docs in repo: `docs/codex`
- Obsidian copies: `Pasukhi/phase-0.md`, `Pasukhi/phase-1.md`, `Pasukhi/phase-2.md`

## Current Status

| Phase | Status | Commit | Note |
| --- | --- | --- | --- |
| Phase 0 | Implemented and committed | `63bd7ed` | [[Pasukhi/phase-0|phase-0]] |
| Phase 1 | Implemented and committed | `93a9700` | [[Pasukhi/phase-1|phase-1]] |
| Phase 2 | Implemented and committed | `c2d7817` | [[Pasukhi/phase-2|phase-2]] |

## Verification Notes

- Phase 0, Phase 1, and Phase 2 compile checks passed.
- Phase 2 unit tests passed: 21 matcher and normalizer tests.
- Runtime database verification is blocked until Docker is installed or local PostgreSQL credentials are corrected.
- Current database blocker: local PostgreSQL rejects `postgres/postgres`.

## Remaining Backlog

- Phase 3: Webhook controllers, signature verification, Meta App setup, ngrok.
- Phase 4: MassTransit, RabbitMQ, inbound queue wiring.
- Phase 5: Full message pipeline, channel providers, persistence, manual replies, conversations UI.
- Phase 6: FAQ matching wired into the message pipeline.
- Phase 7: Automation rules wired into the message pipeline.
- Phase 8: AI integration, prompt builder, safety checker.
- Phase 9: Escalations, resolution workflow, escalation queue UI.
- Phase 10: Settings, AI prompt editor, metrics, analytics dashboard.
- Phase 11: Production deployment, domain, HTTPS, webhook URL.
- Phase 12: Hardening, rate limits, token encryption, security review, load testing.

## Related Maps

- [[Project Map]]
- [[Operations Map]]
