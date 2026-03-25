# Code Review — Address Enrichment POC

> Reviewed by: Claude Sonnet 4.6 (Claude Code)
> Built by: Codex + Priyadarshi Swain

---

## What's Well Done

- **Clean architecture**: Controller → Service → Mapper separation is clear and testable. `IGoogleApiService` interface makes unit testing straightforward.
- **Fallback logic**: The geocoding → places text search fallback in `GetPostalBoundaryTargetAsync` is smart UX. Users don't hit a dead end.
- **Test infrastructure**: The `TestWebApplicationFactory` with stubbed HTTP responses is the right approach — no real API calls, no flakiness.
- **Logging**: Serilog with file rotation is production-ready out of the box.
- **Parallel POI fetching**: `Promise.allSettled()` for airports + ports is the right call.

---

## Concerns Worth Addressing

### High Priority

1. **API keys in `appsettings.json`** — These should not be committed. Move to env vars or user secrets (`dotnet user-secrets`). The maps key being browser-exposed is fine if restricted by HTTP referrer, but the server-side keys shouldn't be in source.

2. **Root component bloat (`app.ts`)** — 20+ state variables in one component is going to hurt. With Angular Signals now stable, this is a natural fit for a reactive state model. At minimum, split into a service that owns state.

### Medium Priority

3. **Search geometry mismatch** — Nearby search uses a 50 km circle, text search uses a 0.45° bounding box (~50 km square but corners are ~70 km away). Probably fine for a POC but inconsistent.

4. **Magic numbers everywhere** — `50_000` meters, `0.45°`, `30_000` monthly requests, `200` char trim. These should be constants with names explaining *why*, not just *what*.

5. **Null safety in mappers** — `ExtractPostalCodeFromComponents()` returns `""` but callers treat it inconsistently. Pick one convention (empty string or nullable) and stick to it.

### Low Priority

6. **No frontend tests** — Haversine and cost calculation logic are pure functions that are trivial to unit test and easy to get wrong.

---

## Architecture Verdict

For a POC: **this is above average**. The intent is clearly "show this works before investing more" and it does that well. The backend structure could go to production with config cleanup and key externalization. The frontend needs a rethink before scaling.
