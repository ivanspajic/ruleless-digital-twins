# Demo Setup

This guide covers the two supported demo paths:

- offline replay mode, with no Home Assistant and no token;
- Home Assistant live mode, with a user-provided `<TOKEN_HA>`.

## Offline Replay Demo

From the repository root:

```powershell
$env:SMARTNODE_MODE = "chatbox-only"
$env:PRICE_PROVIDER = "replay"
$env:PRICE_REPLAY_FILE = "config/price-replay.sample.json"
$env:PRICE_REPLAY_REBASE_TODAY = "true"
$env:DECISION_LOG_PROVIDER = "sqlite"
$env:GOAL_REPOSITORY_PROVIDER = "sqlite"
$env:SETTINGS_STORE_PROVIDER = "sqlite"

dotnet run --project .\SmartNode\SmartNode\SmartNode.csproj
```

In another shell:

```powershell
Invoke-RestMethod -Method Post -Uri http://localhost:8080/api/mapek/tick `
  -ContentType application/json -Body '{"dryRun": true}' | ConvertTo-Json -Depth 8

Invoke-RestMethod -Uri http://localhost:8080/api/mapek/decisions | ConvertTo-Json -Depth 8
```

Serve the dashboard over HTTP:

```powershell
cd .\SmartNode\SmartNode
python -m http.server 8000
```

Then open `http://localhost:8000/dashboard.html`.

## Docker Offline Demo

```powershell
docker compose up -d --build
```

Open:

- `http://localhost:8082/dashboard.html`
- `http://localhost:8082/setup.html`
- `http://localhost:8080/api/mapek/decisions`

Stop the stack:

```powershell
docker compose down
```

Use `docker compose down -v` only when you intentionally want to remove the
named SQLite data volume.

## Home Assistant Live Demo

Start Home Assistant separately and set runtime variables:

```powershell
$env:SMARTNODE_MODE = "full"
$env:HA_URL = "http://localhost:8123"
$env:TOKEN_HA = "<TOKEN_HA>"
$env:HA_BINDINGS_FILE = "config/ha-bindings.showcase.json"
$env:PRICE_PROVIDER = "homeassistant-nordpool"

dotnet run --project .\SmartNode\SmartNode\SmartNode.csproj
```

Validate Home Assistant is reachable:

```powershell
Invoke-RestMethod -Uri http://localhost:8080/api/ha/connection | ConvertTo-Json -Depth 6
Invoke-RestMethod -Uri http://localhost:8080/api/ha/discovery | ConvertTo-Json -Depth 6
```

Use `SmartNode/SmartNode/setup.html` to test the connection, discover entities,
and export reviewed bindings. The exported file contains entity bindings only;
it does not contain `<TOKEN_HA>`.

## Safe Real-Execution Smoke

Real actuation is off by default. To test the gate without touching Home
Assistant, send a request with `dryRun=false` while `MAPEK_ALLOW_EXECUTION` is
unset:

```powershell
Invoke-RestMethod -Method Post -Uri http://localhost:8080/api/mapek/tick `
  -ContentType application/json -Body '{"dryRun": false}' | ConvertTo-Json -Depth 8
```

Expected result: `executed=false` and a warning explaining which safety gate
blocked execution.

## Validation Commands

```powershell
dotnet build .\SmartNode\SmartNode.sln --nologo
dotnet test .\SmartNode\SmartNode.sln --no-build --nologo --filter "FullyQualifiedName~MapekTickEndpointTests|FullyQualifiedName~MapekMonitorServiceTests|FullyQualifiedName~MapekAnalyzerServiceTests|FullyQualifiedName~ExecutionPolicyTests|FullyQualifiedName~HttpHaActionExecutorTests|FullyQualifiedName~AutonomousTick|FullyQualifiedName~ReplayPriceRebaseTests|FullyQualifiedName~InMemoryDecisionLogTests|FullyQualifiedName~SqliteDecisionLogTests|FullyQualifiedName~DecisionLogOptionsTests|FullyQualifiedName~GoalRepositoryOptionsTests|FullyQualifiedName~JsonGoalRepositoryTests|FullyQualifiedName~SqliteGoalRepositoryTests|FullyQualifiedName~GoalEditorEndpointTests|FullyQualifiedName~SafetyAcceptanceMatrixTests|FullyQualifiedName~SafetyPolicyTests|FullyQualifiedName~SqliteExecutionHistoryTests|FullyQualifiedName~ExecutionHistoryOptionsTests|FullyQualifiedName~ExecutionHistoryTests|FullyQualifiedName~SettingsStoreOptionsTests|FullyQualifiedName~InMemorySettingsStoreTests|FullyQualifiedName~SqliteSettingsStoreTests|FullyQualifiedName~SettingsEndpointTests|FullyQualifiedName~HealthEndpointTests|FullyQualifiedName~HaConnectionTests|FullyQualifiedName~HaConnectionEndpointTests|FullyQualifiedName~HaDiscoveryEndpointTests|FullyQualifiedName~HaBindingConfigExporterTests|FullyQualifiedName~HaBindingDraftBuilderTests|FullyQualifiedName~HomeAssistantRegistryTests|FullyQualifiedName~HttpPrefixTests"
python -m pytest tools/hass-to-rdt/tests -v
```

## Troubleshooting

| Symptom | Check |
|---|---|
| API not reachable | Confirm SmartNode is running and listening on `http://localhost:8080/`. |
| Dashboard cannot call API | Serve `SmartNode/SmartNode` with `python -m http.server 8000`. |
| Live HA returns 401 | Replace `<TOKEN_HA>` in the shell; do not commit it. |
| Tick has no live HA state | Check `HA_URL`, `TOKEN_HA`, and `HA_BINDINGS_FILE`. |
| Docker dashboard unreachable | Check `docker compose ps`; API is `8080`, web UI is `8082`. |
