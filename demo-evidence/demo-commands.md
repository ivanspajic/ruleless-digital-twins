# demo-commands — Demo commands (PowerShell)

All the commands to reproduce the MAPE-K demo **offline** (no real Home Assistant,
price from the `replay` provider). Tested on Windows 11 / PowerShell 7. The
SmartNode internal API listens on `http://localhost:8080`.

> **Secrets**: never paste a real token. The placeholders `<TOKEN_HA>` and
> `<HA_URL>` represent local values that **only you** know.

---

## 0. Prerequisites

- .NET 8 SDK installed (`dotnet --version`).
- Terminal opened at the repository root:
  ```powershell
  cd C:\dev\rdt-demo-clean\ruleless-digital-twins-demo
  ```

---

## a. Get back to a clean `main`

```powershell
git switch main
git pull --ff-only
git status --short      # must be empty
```

---

## b. Start SmartNode in replay mode (offline, no HA)

`chatbox-only` starts the HTTP API **without** the blocking MAPE-K loop, without
MongoDB / RabbitMQ / Home Assistant. `replay` provides a price from a local file.

```powershell
$env:SMARTNODE_MODE = 'chatbox-only'
$env:PRICE_PROVIDER = 'replay'
$env:TOKEN_HA       = ''            # no HA for this demo
dotnet run --project .\SmartNode\SmartNode\SmartNode.csproj
```

Leave this window open. Wait for the line:

```
Internal API listening on http://localhost:8080/
```

> Open a **second** PowerShell window for the commands below.

---

## c. Enable the today-dated replay price

Without it, the replay file (dated to a fixed day) does not cover "now" and
`currentPriceNokPerKwh` stays `null`. The flag shifts the slots to the current day
(prices unchanged, only the dates move).

Restart SmartNode (Ctrl+C in its window, then):

```powershell
$env:SMARTNODE_MODE          = 'chatbox-only'
$env:PRICE_PROVIDER          = 'replay'
$env:PRICE_REPLAY_REBASE_TODAY = 'true'
$env:TOKEN_HA                = ''
dotnet run --project .\SmartNode\SmartNode\SmartNode.csproj
```

Check the current price:

```powershell
Invoke-RestMethod -Method Get -Uri 'http://localhost:8080/api/price' |
  ConvertTo-Json -Depth 6
```

---

## d. Run a manual MAPE-K tick (dry-run)

```powershell
$tick = Invoke-RestMethod -Method Post `
  -Uri 'http://localhost:8080/api/mapek/tick' `
  -ContentType 'application/json' `
  -Body '{"dryRun": true}'

$tick | ConvertTo-Json -Depth 8
```

What to look at in the response:
- `activeGoals`: the loaded goals;
- `simulatedScenarios`: 3 **scored** scenarios;
- `selectedPlan.scenarioId`: the winner (typically `heat-now`);
- `selectedPlan.actions[].executed = false` (dry-run);
- `decision`: the compact trace.

Save the output (see `outputs/`):

```powershell
$tick | ConvertTo-Json -Depth 8 |
  Out-File -Encoding utf8 .\demo-evidence\outputs\tick-dryrun-response.json
```

---

## e. Query the decision log

```powershell
Invoke-RestMethod -Method Get -Uri 'http://localhost:8080/api/mapek/decisions' |
  ConvertTo-Json -Depth 8
```

`count` increases at every tick; decisions are returned **newest first**.

---

## f. Start the autonomous mode (dry-run)

Restart SmartNode with the loop enabled (short interval for the demo):

```powershell
$env:SMARTNODE_MODE            = 'chatbox-only'
$env:PRICE_PROVIDER            = 'replay'
$env:PRICE_REPLAY_REBASE_TODAY = 'true'
$env:MAPEK_AUTONOMOUS          = 'true'
$env:MAPEK_TICK_INTERVAL_SECONDS = '5'
$env:TOKEN_HA                  = ''
dotnet run --project .\SmartNode\SmartNode\SmartNode.csproj
```

In the SmartNode window, you see:

```
MAPE-K autonomous mode ENABLED (dry-run only, interval 5s).
MAPE-K autonomous tick #1 ok.
MAPE-K autonomous tick #2 ok.
...
```

---

## g. Check that decisions accumulate on their own

**Without sending any manual tick**, wait ~20 s then:

```powershell
$d = Invoke-RestMethod -Method Get -Uri 'http://localhost:8080/api/mapek/decisions'
"count = $($d.count)"
$d.decisions | Select-Object -First 5 |
  ForEach-Object { "[$($_.timestamp)] $($_.selectedScenario) dryRun=$($_.dryRun) executed=$($_.actions[0].executed)" }
```

`count` must rise on its own; every decision is `dryRun=true`.

Save it:

```powershell
$d | ConvertTo-Json -Depth 8 |
  Out-File -Encoding utf8 .\demo-evidence\outputs\decisions-after-autonomous-loop.json
```

---

## h. Test the safe refusal of real execution

Real execution is **fail-closed**: it only happens when **all** the gates hold.
This demonstrates the refusal when a gate is missing (here we request `dryRun=false`
but without enabling the master switch).

With a SmartNode started **without** `MAPEK_ALLOW_EXECUTION`:

```powershell
$denied = Invoke-RestMethod -Method Post `
  -Uri 'http://localhost:8080/api/mapek/tick' `
  -ContentType 'application/json' `
  -Body '{"dryRun": false}'

"forced dryRun to true ? $($denied.dryRun -eq $true)"
$denied.warnings
$denied | ConvertTo-Json -Depth 8 |
  Out-File -Encoding utf8 .\demo-evidence\outputs\protected-execution-denied-response.json
```

Expected: `dryRun = true`, a warning explaining that real execution was **blocked**
(missing gate), `executed = false`.

> To demonstrate *authorized* real execution, you need a reachable Home Assistant
> + `MAPEK_ALLOW_EXECUTION=true` + `TOKEN_HA` + the entity/service on
> `MAPEK_ALLOWED_ENTITIES` / `MAPEK_ALLOWED_SERVICES`. See `demo-scenario.md`.

---

## i. Clean shutdown

In the SmartNode window: `Ctrl+C`. The autonomous loop stops cleanly (message
`MAPE-K autonomous tick stopped after N iteration(s).`).

---

## Environment variables recap

| Variable | Role | Demo |
|---|---|---|
| `SMARTNODE_MODE` | `chatbox-only` = API without the blocking MAPE-K loop | `chatbox-only` |
| `PRICE_PROVIDER` | price source | `replay` |
| `PRICE_REPLAY_REBASE_TODAY` | shifts the sample onto today | `true` |
| `MAPEK_AUTONOMOUS` | enables the autonomous loop | `true` |
| `MAPEK_TICK_INTERVAL_SECONDS` | interval (min 5s) | `5` |
| `TOKEN_HA` | HA token (empty offline) | `<TOKEN_HA>` |
| `MAPEK_ALLOW_EXECUTION` | master switch for real execution | off in the demo |
| `MAPEK_ALLOWED_ENTITIES` | entity allowlist | `<entities>` |
| `MAPEK_ALLOWED_SERVICES` | service allowlist | `<services>` |
