# ADR 0004 - Dashboard and safety-first autonomy

- **Status:** Accepted
- **Date:** 2026-06-01
- **Relates to:** dashboard and the MAPE-K runtime
  work (autonomous loop, fail-closed execution); builds on
  [ADR 0002](0002-storage-providers.md) and [ADR 0003](0003-goals-api-and-read-write-split.md).

## Context

Once goals are durable ([ADR 0002](0002-storage-providers.md)), manageable over an
API ([ADR 0003](0003-goals-api-and-read-write-split.md)), and the MAPE-K loop can
tick on its own, a product needs a way to **see** what the system is doing without
a terminal, and it must do so *without* turning the demo into something that can
silently change the home. The two concerns are intertwined: the moment there is a
"dashboard with buttons", the safety story has to be explicit, not assumed.

The constraint is therefore: surface the live state and let an evaluator trigger a
*safe* action, while guaranteeing that neither the dashboard nor the autonomous
loop can actuate Home Assistant unless a human has explicitly, separately enabled
real execution.

## Decision

### 1. The dashboard is read-only by construction

[`SmartNode/SmartNode/dashboard.html`](../../SmartNode/SmartNode/dashboard.html) is a
static page that, on load, only **reads**:

- `GET /api/goals` — the active goals (via [ADR 0003](0003-goals-api-and-read-write-split.md)).
- `GET /api/mapek/decisions` — the recent decision log (via [ADR 0002](0002-storage-providers.md)).

The single action on the page is a **Run dry tick** button, which calls
`POST /api/mapek/tick` with `{ "dryRun": true }`. There is no control on the page
that can produce real actuation: the dashboard never sends `dryRun=false`, never
issues `POST`/`DELETE /api/goals`, and never calls `/api/call_service`.

### 2. The autonomous loop is always dry-run

When `MAPEK_AUTONOMOUS` is truthy the tick runs on a background timer
(`MAPEK_TICK_INTERVAL_SECONDS`, min 5 s). This loop always observes, analyzes,
scores, plans, logs, and **never** actuates Home Assistant, even if
`MAPEK_ALLOW_EXECUTION=true`. Autonomy decides and records; it does not act.

### 3. Real execution is fail-closed, opt-in, and orthogonal to the UI

Real actuation from `POST /api/mapek/tick` only happens when **all** cumulative
gates hold:

1. `MAPEK_ALLOW_EXECUTION` truthy (master switch), **and**
2. the request explicitly sends `dryRun=false`, **and**
3. `TOKEN_HA` is set, **and**
4. the action's `entity_id` is on `MAPEK_ALLOWED_ENTITIES`, **and**
5. the action's `domain.service` is on `MAPEK_ALLOWED_SERVICES`.

If any gate is missing the tick stays dry-run (`executed=false`) and the response
explains why. The allowlists are authoritative: empty allowlists mean nothing can
be actuated. This path is reachable only by an explicit API call, never from the
dashboard.

## Consequences

- An evaluator can open one page and see goals, decisions, and a live tick without
  reading the terminal or touching a token.
- The dashboard is safe to demo to anyone: the worst it can do is run a dry tick
  that logs a decision. It cannot change the home.
- The "autonomy never actuates" rule keeps the demo honest: a continuously ticking
  system is **deciding**, not **adapting the home**, which is exactly how the
  README's status table frames it.
- The safety guarantee is a property of the gate, not of the UI: hiding a button
  was never the protection. The same fail-closed contract protects the API.

## Status of related work

- **Implemented:** the read-only dashboard, the autonomous dry-run loop, and the
  five-gate fail-closed execution policy, all covered by offline tests
  (`ExecutionPolicyTests`, `AutonomousTick*`, `MapekTickEndpointTests`).
- **Future:** goal editing from the dashboard (the API in
  [ADR 0003](0003-goals-api-and-read-write-split.md) is ready), live decision
  streaming, and richer visualisations.
