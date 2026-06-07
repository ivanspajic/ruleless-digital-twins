# demo-evidence — SmartNode run captures

This directory holds end-to-end evidence that the fork's **SmartNode**
coordinator binary actually runs: real `dotnet run` invocations against
several preconfigured `appsettings-*.json` profiles, with the **full stdout,
stderr, exit code, and the appsettings file the run consumed** preserved for
each run.

It is **not** a screenshot of an HTTP dashboard or a chatbot demo (that lived
in the original demo project on top of an HTTP API and is not part of this
fork). The fork ships the upstream-style CLI coordinator: it reads
`--appsettings`, runs an ontology + inference + FMU simulation MAPE-K loop for
N rounds, prints the chosen actuation per cycle, then exits.

## What this proves

- The fork **builds and runs** end-to-end on Windows 11 with the vendored
  Femyou, the vendored FMUs, and the inference engine JAR.
- The MAPE-K loop produces deterministic `Actuator state` decisions per cycle
  across **four different control regimes** (proactive 1-ahead, proactive
  3-ahead, reactive, bang-bang) on the M370 room scenario.
- The incubator environment **fails closed** with a clear AMQP error when no
  incubator twin is publishing on RabbitMQ — i.e. no silent hang.

## Profiles captured

| Profile | Env | Rounds | Look-ahead | Mode | Ruleless | Exit | Decisions |
|---|---|---|---|---|---|---|---|
| [`default`](runs/default/summary.md) | `roomM370` | 4 | 1 | proactive | yes | 0 | 4 |
| [`bang-bang`](runs/bang-bang/summary.md) | `roomM370` | 20 | 1 | proactive | **no** | 0 | 20 |
| [`reactive-1-ahead-20`](runs/reactive-1-ahead-20/summary.md) | `roomM370` | 20 | 1 | **reactive** | yes | 0 | 20 |
| [`proactive-3-ahead-20-fuzz5`](runs/proactive-3-ahead-20-fuzz5/summary.md) | `roomM370` | 20 | **3** | proactive | yes | 0 | 7 (multi-step paths) |
| [`incubator`](runs/incubator/summary.md) | `incubator` | ∞ | 3 | proactive | yes | **35** | 0 (no AMQP twin) |

The "Decisions" column reflects what was extracted from each stdout — the
**ruleless MAPE-K** path emits a `Chosen optimal path, Actuation actions:`
block per cycle. The **bang-bang** path is non-ruleless (`UseRulelessMethod=false`)
and instead emits `Actuating actuator <uri> with state N.` lines, three per
cycle (Heater, FloorHeating, Dehumidifier), so 20 cycles → 60 raw lines → 20
decisions in the summary.

## How the runs were produced

All runs share the same recipe; only `--appsettings` changes (and `default`
omits it to use `appsettings.json`):

```powershell
# Bring up the supporting services the M370 case base hooks may dial out to.
# Not strictly required for these MAPE-K-only runs, but mirrors README setup.
docker compose -f services/docker-compose.demo.yml up -d mongodb rabbitmq

# Clean any CSVs left from a previous "SaveMapekCycleData: true" run, otherwise
# the next run with that flag will trip an IOException on a locked file.
Remove-Item state-data\*.csv -ErrorAction SilentlyContinue

# Run a profile
cd SmartNode\SmartNode
dotnet run --no-launch-profile -- `
  --basedir "C:\Users\ln20col56\Projets\rdt-fork-clean" `
  --appsettings appsettings-rdt-reactive-1-ahead-20-cycles-fuzziness-5.json
```

The exact `appsettings-used.json` snapshot is preserved next to each
`stdout.log` in `runs/<profile>/`.

## Files per run

| File | Purpose |
|---|---|
| `appsettings-used.json` | snapshot of the profile the run consumed |
| `stdout.log` (or `stdout.log.gz` when > 2 MB) | full stdout of `dotnet run` |
| `stderr.log` | full stderr (empty on the four successful runs) |
| `exit-code.txt` | process exit code |
| `summary.md` | per-run table + parsed cycle decisions |

The `proactive-3-ahead-20-fuzz5` run produced ~19 MB of stdout (20 cycles ×
3-cycle look-ahead × per-step verbose logging) so its log is gzipped. Decompress
with `gunzip -k stdout.log.gz` or `gzip -d -k stdout.log.gz`.

## Notable observations

- **Default vs reactive on M370**: both end with `Heater=0, FloorHeating={0|1},
  Dehumidifier=0` — proactive 1-ahead and reactive 1-ahead converge to the
  same trivial steady-state on this short horizon. The reactive run shows the
  controller still iterating decisions every cycle even though the system is
  already inside the optimal band.

- **Proactive 3-ahead**: each "decision" extracted is actually a **3-step
  path** (look-ahead = 3), which is why only 7 path blocks were extracted from
  the 20-round run — the ruleless planner emits one full path per planning
  step rather than per environment cycle.

- **Bang-bang**: 60 raw `Actuating actuator ... with state N.` lines across 20
  cycles, grouped to 20 cycle-decisions in the summary. Behaviour walks
  through every `(Heater, FloorHeating, Dehumidifier)` combination as
  expected for the non-ruleless controller.

- **Incubator**: `Process terminated. Assertion failed. No data received from
  Incubator AMQP.` — RabbitMQ is up (per `docker compose up`) but no incubator
  hardware twin (nor a stand-in simulator) is publishing on the AMQP
  exchange, so the `AmqSensor` asserts out after its read timeout. Documented
  expected behaviour; matches the test-side skip on `SimulateFromAMQ`.

## Regenerating evidence

This entire directory is a **point-in-time capture**. Anyone with the
prerequisites in the top-level [`readme.md`](../readme.md#local-development-windows)
section can replay the recipe above; the `state-data\*.csv` cleanup step matters
between runs that have `SaveMapekCycleData: true`.

## Security notes

- The runs do not touch Home Assistant (the M370 profiles use the dummy
  factory) so no HA token leaks here.
- `MongoDB` and `RabbitMQ` brought up by `docker-compose.demo.yml` are
  loopback-only with documented credentials (`incubator/incubator`) — fine
  for local dev, not safe for a public host.
