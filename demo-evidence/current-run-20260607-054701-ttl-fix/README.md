# SmartNode run evidence - TTL fix

Run date: 2026-06-07

## Scope

This run followed the correction requested for the missing TTL file:

- copy the Home Assistant RDT generation helpers into `C:\Users\ln20col56\Projets\rdt-fork-clean\models-and-rules`;
- generate and verify `homeassistant-ha-inferred.ttl`;
- copy the required generated TTL back into the active SmartNode workspace;
- relaunch SmartNode and retest API/UI behavior;
- capture screenshots and logs as proof.

## Files generated or copied

In `C:\Users\ln20col56\Projets\rdt-fork-clean\models-and-rules`:

- `hacvt_rdt.py`
- `hacvt.py`
- `ConfigSource.py`
- `homeassistant-ha-instance.ttl`
- `homeassistant-ha-inferred.ttl`

In `C:\Users\ln20col56\Projets\ruleless-digital-twins-demo\models-and-rules`:

- `homeassistant-ha-instance.ttl`
- `homeassistant-ha-inferred.ttl`

The pure live HA export was saved as:

- `homeassistant-ha-instance.live-export.ttl`

Important finding: the pure live HA export describes discovered HA entities, but it does not contain the scenario simulation data (`meta:FmuModel`, `meta:hasSimulationModel`, `meta:OptimalCondition`). The final inferred file was therefore generated from the curated scenario instance model, not from the pure live export.

## Commands represented by logs

- `hacvt_rdt.log`: live Home Assistant RDT export ran successfully.
- `inference-engine.log`: inference from the live export ran, but that model is not enough for full MAPE-K planning.
- `inference-engine-curated.log`: inference from the curated scenario model ran and generated actuation actions.
- `smartnode-final.out.log`: final SmartNode run after the missing TTL was present.

## What now works

- The previous `FileNotFoundException` for `models-and-rules/homeassistant-ha-inferred.ttl` is gone.
- `GET /api/health` returns `200`.
- `GET /api/ready` returns `200` and all local providers are ready.
- `GET /api/model/validation` returns `200` with `result=PASS` when `HA_BINDINGS_FILE=config/ha-bindings.showcase.json`.
- `POST /api/ha/connection` returns `200`, Home Assistant `2025.9.3`, `99` entities.
- `POST /api/mapek/tick` returns `200` in dry-run mode and selects `heat-now`.
- Dashboard loads and shows Home Assistant connected after the runtime HA connection test.

## What still does not work

- The continuous full MAPE-K loop still fails after the TTL fix.
- It now fails later than before: the missing file is fixed, but the planner generates `0 simulation paths`.
- The repeated runtime error is:
  - `Generated a total of 0 simulation paths.`
  - `System.ArgumentNullException: Value cannot be null. (Parameter 'source')`
  - location: `MapekPlan.cs:line 88`
- The log also shows the FMU query returning `(0)` during the runtime loop:
  - `SELECT ?fmuModel ?fmuFilePath ?simulationFidelitySeconds ... (0)`

## Screenshots

- `01-dashboard-after-ttl-fix.png`
- `02-dashboard-after-ha-runtime-connect.png`

## API proof files

- `api-final-health.json`
- `api-final-ready.json`
- `api-final-product-status-after-ha-post.json`
- `api-final-model-validation.json`
- `api-final-ha-connection-post.json`
- `api-final-mapek-tick.json`
- `api-final-decisions.json`

## Conclusion

The missing TTL file issue is fixed. SmartNode can start its API, validate bindings, connect to Home Assistant, and run the product dry-run tick. The remaining blocker is a separate planner/runtime issue in the continuous MAPE-K loop: it finds no simulation paths and then crashes on a null source in `MapekPlan.Plan`.
