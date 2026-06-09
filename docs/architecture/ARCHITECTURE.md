# Architecture

SmartNode is a .NET 8 service that connects Ruleless Digital Twins concepts to
Home Assistant. It exposes an HTTP API, runs a MAPE-K loop, reads Home
Assistant and price-provider state, stages actions, and only executes real Home
Assistant service calls through a fail-closed safety layer.

## Component Diagram

```text
Dashboard / setup pages
        |
        | HTTP JSON
        v
SmartNode API (.NET 8)
        |
        +-- MAPE-K loop
        |     +-- Monitor: Home Assistant state + energy price
        |     +-- Analyze: findings from state and goals
        |     +-- Plan: scenario scoring and staged actions
        |     +-- Execute: fail-closed Home Assistant actuation
        |     +-- Knowledge: goals, bindings, decisions, ontology context
        |
        +-- Persistence
        |     +-- decision log: memory or SQLite
        |     +-- goals: JSON read-only or SQLite mutable
        |     +-- settings: memory or SQLite, non-secret only
        |     +-- execution history: SQLite by default
        |
        +-- Home Assistant integration
        |     +-- state reads
        |     +-- services
        |     +-- discovery and binding export
        |
        +-- Price providers
              +-- Home Assistant Nord Pool
              +-- replay JSON
              +-- fakepool legacy path
```

## Main Modules

| Module | Responsibility |
|---|---|
| `SmartNode/SmartNode/Program.cs` | Host startup, DI registration, HttpListener API. |
| `SmartNode/SmartNode/Mapek/` | MAPE-K endpoint, monitoring, analysis, autonomy, execution safety. |
| `SmartNode/SmartNode/Services/Decisions/` | Decision-log providers. |
| `SmartNode/SmartNode/Services/Goals/` | Goal repository and goal editor API logic. |
| `SmartNode/SmartNode/Services/Settings/` | Non-secret settings store. |
| `SmartNode/SmartNode/Services/Execution/` | Durable execution history for cooldown/rate limits. |
| `SmartNode/SmartNode/Services/HomeAssistant/` | Connection, discovery, and binding export helpers. |
| `SmartNode/SmartNode/Services/Simulation/` | Future-scenario simulator seam. |
| `config/` | Demo bindings, replay prices, and example goals. |
| `tools/hass-to-rdt/` | Home Assistant to RDT export tooling. |

## Persistence

Local persistence is SQLite-first where durability is required and memory/JSON
where the demo should remain lightweight:

- decisions: `DECISION_LOG_PROVIDER=memory|sqlite`;
- goals: `GOAL_REPOSITORY_PROVIDER=json|sqlite`;
- settings: `SETTINGS_STORE_PROVIDER=memory|sqlite`;
- execution history: `MAPEK_EXECUTION_HISTORY_PROVIDER=sqlite|memory`.

SQLite files are runtime state and are ignored by Git.

## Deployment

Supported paths:

- local `.NET` run from the repository root;
- Docker offline demo through `docker-compose.yml`;
- Home Assistant live mode with a user-provided `<TOKEN_HA>` and reviewed
  bindings.

The production Home Assistant add-on path is future work.

## Related Docs

- [MAPE-K Loop](MAPEK_LOOP.md)
- [Home Assistant Integration](HOME_ASSISTANT_INTEGRATION.md)
- [Safety Model](SAFETY_MODEL.md)
- [Configuration](../developer/CONFIGURATION.md)
