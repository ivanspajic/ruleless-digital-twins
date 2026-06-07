# demo-scenario — MAPE-K verification scenario

Step-by-step technical scenario for verifying the MAPE-K loop. The exact
commands are in [`demo-commands.md`](demo-commands.md).

---

## a. Context

SmartNode is a **MAPE-K** controller (Monitor → Analyze → Plan → Execute →
Knowledge) for digital twins. It drives Home Assistant entities from **user
goals** and an energy **price signal**, all under a **dry-run** guard by default.

For reproducible verification, we run **offline**: no real Home Assistant, price provided by the
`replay` provider (recorded data, shifted onto the current day).

## b. User goal

A comfort goal is loaded from `config/user-goals.example.json`:

> `demo-evening-comfort`: from 18:00, if I am home, aim for 21°C in the living room,
> favouring low-price hours (≤ 2.5).

It declares an associated Home Assistant action:
`climate.set_temperature` on `climate.demo_living_room` → 21°C.

## c. Observation (Monitor)

At every tick, the Monitor produces the observed state:
- snapshot of the Home Assistant entities (empty offline → explicit warning);
- current price (`currentPriceNokPerKwh`) from the `replay` provider.

**Show**: the response contains `observedState` with a non-null current price
(thanks to `PRICE_REPLAY_REBASE_TODAY=true`).

## d. Analysis (Analyze)

The Analyzer emits **coded findings** (machine-readable), e.g.:
- `ha-snapshot-unavailable` (offline);
- `current-price-...` depending on price availability;
- `active-goals-not-planned`, `no-ha-action-executed`.

**Show**: the `analysis.findings` block.

## e. Scenario scoring (Plan, step 1)

The simulator produces **3 deterministically scored scenarios**:

| Scenario | Wins when | Action |
|---|---|---|
| `do-nothing` | temperature already at target | none |
| `heat-now` | too cold **and** price acceptable | the goal's action |
| `wait-cheaper` | too cold **but** price too high | none (defers) |

**Show**: `simulatedScenarios` with the `score` values (e.g. heat-now 0.80 >
do-nothing 0.20 > wait-cheaper 0.10).

## f. Selected plan (Plan, step 2)

The planner selects the **highest-scoring** scenario (`argmax`), not the first one.

**Show**: `selectedPlan.scenarioId = heat-now`, with a readable `rationale`.

## g. Home Assistant action in dry-run (Execute)

The winning plan projects the goal's action into an `HaAction`, but in **dry-run**:
`executed = false`, **nothing is sent** to Home Assistant.

**Show**: `selectedPlan.actions[0]` =
`climate.set_temperature` / `climate.demo_living_room` / `executed: false`.

## h. Decision log (Knowledge)

Every tick writes a **compact trace** into an internal log, queryable via
`GET /api/mapek/decisions` (newest first).

**Show**: `/api/mapek/decisions` with `count` increasing at every tick.

## i. Autonomous tick

Enable `MAPEK_AUTONOMOUS=true` (interval 5s). The system **ticks on its own**: you
no longer send any command, and the decision log **fills automatically**.

**Show**: with no curl, `count` rises (1 → 2 → 3 → 4…), decisions spaced by the
interval, all `dryRun=true`.

## j. Real-execution safety

Real Home Assistant execution is **fail-closed**. It only happens when **all 5
cumulative gates** hold:

1. `MAPEK_ALLOW_EXECUTION` enabled (master switch);
2. request `dryRun=false`;
3. `TOKEN_HA` present;
4. entity on `MAPEK_ALLOWED_ENTITIES`;
5. service on `MAPEK_ALLOWED_SERVICES`.

If **a single one** is missing → no real action, `executed=false`, explicit
warning. On top of that, the **autonomous** loop stays **always dry-run**, even if
the master switch is enabled.

**Show**: a `POST {"dryRun": false}` without the master switch → the response stays
`dryRun=true` with a warning "Real execution blocked: …".

---

## Reproduction order

1. Start SmartNode (b + c).
2. One manual tick (d) → comment scenarios + plan + dry-run action (e→g).
3. The decision log (e/h).
4. Enable autonomous (f) → show the log filling on its own (i).
5. Show the real-execution refusal (j).
6. Verify the safety guarantees.
