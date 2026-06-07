# Home Assistant → RDT exporter

CLI tool that connects to a [Home Assistant](https://www.home-assistant.io)
instance and exports its static structure (devices, entities, locations) as a
Turtle/OWL file compatible with the **ruleless-digital-twins** SmartNode
ontology (SOSA/SSN + `rdt:` namespace).

This is a slimmed-down, RDT-only fork of the upstream
[HASS-to-OWL-exporter](https://github.com/Edkamb/HASS-to-OWL-exporter)
maintained by HVL (Volker Stolz, Eduard Kamburjan, Fernando Macías,
Adam Cheng). The Flask/Celery web frontend has been left out — only the
command-line backend is shipped here, since that is what the demo uses.

## Files

- `hacvt_rdt.py` — RDT/SOSA backend, online (talks to a live HA instance).
- `hacvt.py` — Base class shared with the upstream SAREF backend; imported
  by `hacvt_rdt`.
- `ConfigSource.py` — CLI/config helper used by both modules.
- `gen_bindings.py` — WP1 offline draft generator (TTL → bindings JSON
  draft). See [Generating draft bindings](#generating-draft-bindings).
- `binding.py` — pure helper module used by `gen_bindings.py`.
- `tests/` — pytest suite for `binding.py` and `gen_bindings.py`. Runs
  fully offline; no live HA, no `TOKEN_HA`.
- `requirements-cli.txt` — Minimal PyPI deps for the CLI path
  (`pytest` is included so the WP1 tests can run from the same venv).

## Setup

```bash
cd tools/hass-to-rdt
python -m venv .venv
.venv\Scripts\activate          # Windows
# source .venv/bin/activate     # Linux/macOS
pip install -r requirements-cli.txt
```

Note: `homeassistant` itself is installed as a regular PyPI package and
pulls in most transitive deps (`voluptuous`, `aiohttp`, `awesomeversion`,
…). Install can take a couple of minutes the first time.

## Usage

You need a Home Assistant URL and a long-lived access token
([how to create one](https://developers.home-assistant.io/docs/auth_api/#long-lived-access-token)).
Store the token in an environment variable — **do not pass the literal
token on the command line**.

```powershell
$env:TOKEN_HA = "eyJhbGc..."
$env:HA_URL   = "http://localhost:8123/api/"
$env:RDT_NS   = "http://www.semanticweb.org/rayan/ontologies/2025/ha/"

python hacvt_rdt.py $env:HA_URL TOKEN_HA `
    --namespace $env:RDT_NS `
    --out ../../models-and-rules/homeassistant-instance.ttl
```

The second positional argument is the **name** of the env var that
holds the token, not the token itself.

## Options

```
usage: hacvt_rdt.py [-h] [-d [DEBUG]] [-n NAMESPACE] [-o OUT]
                    [-p [platform* ...]] [-m IP] [-c ca.crt]
                    url TOKENVAR

positional arguments:
  url               HA API root, e.g. http://localhost:8123/api/.
  TOKENVAR          Name of the env var holding the long-lived token.

options:
  -n, --namespace   RDF namespace for individuals (default RDT_NS).
  -o, --out         Output filename (default ha.ttl).
  -p, --privacy     Enable privacy filter; with no list, sensible default.
  -m, --mount       ForcedIPHTTPSAdapter override IP for internal HA URLs.
  -c, --certificate Path to CA cert; "None" disables validation.
```

## Output

The generated `.ttl` plugs straight into the SmartNode pipeline — copy or
symlink it into `models-and-rules/homeassistant-instance.ttl`. The
inference engine (`inference-engine/ruleless-digital-twins-inference-engine.jar`)
will pick it up on the next MAPE-K cycle.

## Generating draft bindings

`hacvt_rdt.py` produces a TTL describing the world *as discovered* in
Home Assistant. SmartNode does not consume that TTL directly — it loads
`config/ha-bindings.<profile>.json`, a hand-curated file that decides
**which** entities are part of a given research scenario, **which**
sensorUri/procedureUri pairs they map to, and **which** actuators are
driveable (`Light`, `Switch`, `InputBoolean`, `InputSelect`,
`InputNumber`).

Until WP1 the bindings file was written entirely by hand. WP1 closes
that gap with a *semi-automatic* generator. The generator extracts
candidates from the TTL into a draft JSON; a human still has to review
it before the file becomes the binding SmartNode loads.

### Pipeline

```
Home Assistant
  → tools/hass-to-rdt/hacvt_rdt.py
  → models-and-rules/homeassistant-ha-instance.ttl
  → tools/hass-to-rdt/gen_bindings.py            (offline)
  → config/ha-bindings.<profile>.draft.json       (generated)
  → manual review (mandatory)
  → config/ha-bindings.<profile>.json             (final)
  → SmartNode / MAPE-K
```

### Command

```powershell
python tools\hass-to-rdt\gen_bindings.py `
    --ttl models-and-rules\homeassistant-ha-instance.ttl `
    --profile testlab
```

Common options:

- `--ttl PATH` — input TTL (required).
- `--profile NAME` — profile name embedded in the JSON (required).
- `--out PATH` — explicit output path; must end with `.draft.json`.
- `--output-dir DIR` — output directory when `--out` is omitted
  (default: `config`).
- `--platform IRI` — platform IRI written into the JSON (default
  `ha:HomeAssistantTest`).
- `--overwrite` — allow overwriting an existing `*.draft.json`.
- `--include-unknown` — *off by default*. Also place actuators with an
  unsupported HA domain (`climate.*`, `cover.*`, …) in `actuators[]`
  with `haKind = null`. **A draft generated with this flag will not
  pass `HaBindingsLoader` validation.** Use it only when you want to
  see the candidate inline rather than under `ignoredCandidates[]`.

By default the draft contains in `actuators[]` only entries whose
`haKind` is in the `HomeAssistantActuator.ActuatorKind` enum
(`InputBoolean`, `InputSelect`, `Light`, `Switch`, `InputNumber`).
Candidates with an unsupported domain are recorded under a top-level
`ignoredCandidates[]` field for the human reviewer:

```json
"ignoredCandidates": [
  {
    "type":         "actuator",
    "actuatorUri":  "http://example.org/lab/LivingRoomClimate",
    "haEntityId":   "climate.living_room",
    "reason":       "HA domain 'climate' is not driveable by SmartNode (not in HomeAssistantActuator.ActuatorKind)"
  }
]
```

`HaBindingsConfig.cs` does not declare this property and
`System.Text.Json` silently ignores unknown JSON properties at the
options used by `HaBindingsLoader.Load`, so the field never reaches
SmartNode runtime.

The generator **refuses** to write a filename that does not end with
`.draft.json` — it must never clobber the curated final file by accident.

### What still requires a human

The generator only does what a heuristic can do safely:

- include every individual typed (transitively) as `sosa:Sensor` /
  `sosa:Actuator` that carries an `rdt:hasIdentifier` triple,
- guess `haKind` from the HA domain (`light.*` → `Light`, `switch.*` →
  `Switch`, `input_boolean.*` → `InputBoolean`, `input_number.*` →
  `InputNumber`, `input_select.*` → `InputSelect`).

It cannot decide:

- which entities are *relevant to this scenario* (a temperature sensor
  in another room may be noise),
- which sensors should be `Constant` / `GeneralConstant` / `DummyEnergy`
  rather than `HomeAssistant`,
- the right binding for `climate.*`, `cover.*`, or any other domain the
  C# `HomeAssistantActuator.ActuatorKind` enum does not currently
  support — these are listed under `ignoredCandidates[]` for review,
- whether two `sensorUri` candidates should actually point to the same
  HA entity (the showcase profile does this on purpose for demo).

After running the generator you must:

1. open `config/ha-bindings.<profile>.draft.json`,
2. delete entries that are not part of the scenario,
3. for each item under `ignoredCandidates[]`, decide either to drop it
   or to wire it to a Dummy implementation (and add a corresponding
   actuator entry by hand),
4. confirm `kind` values (`HomeAssistant` vs. `Constant`/`Dummy*`),
5. remove the `ignoredCandidates[]` field if you want a clean final
   file (it is not required — `HaBindingsLoader` ignores it),
6. rename the file to `config/ha-bindings.<profile>.json`,
7. let SmartNode validate it on next start (the C# loader is strict).

### Tests

```powershell
python -m pytest tools\hass-to-rdt\tests -v
```

The tests are strictly offline: a `block_network` fixture refuses any
TCP connection in the test process. No `TOKEN_HA` and no live HA
instance is required.

### Security

- The generator never reads `TOKEN_HA` / `HA_URL` and makes no network
  call — TTL parsing is local.
- The CLI refuses any argument whose value looks like a Bearer token or
  a JWT (string starting with `Bearer ` or `eyJ`).
- `*.draft.json` files are in `.gitignore` by default. Drafts may
  reveal entity names from a private HA instance; review before
  committing.

## Differences vs. upstream `hacvt.py`

- Uses **SOSA** (`http://www.w3.org/ns/sosa/`) and **SSN** namespaces
  instead of SAREF / S4BLDG.
- Maps HA domains → `sosa:Sensor` / `sosa:Actuator` / `sosa:Platform`.
- Attaches `rdt:hasIdentifier` carrying the raw HA `entity_id` (so
  SmartNode can call back to HA without a separate mapping table).
- Queries `/api/services` to enumerate possible actuator states and
  emits `rdt:hasActuatorState` triples.
- Uses `sosa:ObservableProperty` for sensor measurement targets.
- No SAREF / S4BLDG / `homeassistantcore.rdf` side-effects.

## License

Inherits the upstream license from
[HASS-to-OWL-exporter](https://github.com/Edkamb/HASS-to-OWL-exporter).
See the repo root `LICENSE.md` for the RDT project license. If you
redistribute, keep the credit lines above.
