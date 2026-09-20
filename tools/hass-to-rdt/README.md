# Home Assistant → SOSA/RDT exporter

CLI tool that connects to a [Home Assistant](https://www.home-assistant.io)
instance and exports its structure (entities, sensors, actuators, possible
actuator states) as a Turtle file using the **SOSA/SSN** vocabulary plus the
`rdt:` namespace, ready to feed the inference engine.

For each entity it emits `a sosa:Sensor` / `a sosa:Actuator`, attaches the raw
Home Assistant `entity_id` via `rdt:hasIdentifier`, and enumerates possible
actuator states (`rdt:hasActuatorState`) discovered from `/api/services` and the
entity attributes (`fan`, `climate`, `cover`, `input_select`, …).

## Setup

```bash
cd tools/hass-to-rdt
python -m venv .venv
.venv\Scripts\activate          # Windows
# source .venv/bin/activate     # Linux/macOS
pip install -r requirements-cli.txt
```

The `homeassistant` package pulls in most transitive deps; first install can
take a couple of minutes.

## Usage

You need a Home Assistant API URL and an **admin** long-lived access token
([how to create one](https://developers.home-assistant.io/docs/auth_api/#long-lived-access-token)).
Store the token in an environment variable — never pass the literal token on the
command line.

```powershell
$env:TOKEN_HA = "<your-long-lived-token>"

python hacvt_rdt.py http://localhost:8123/api/ TOKEN_HA `
    --namespace http://example.org/ha/ `
    --out homeassistant-ha-instance.ttl
```

The second positional argument is the **name** of the env var holding the token,
not the token itself.

### Options

```
usage: hacvt_rdt.py [-h] [-n NAMESPACE] [-o OUT] [-p [platform* ...]]
                    [-m IP] [-c ca.crt] url TOKENVAR

  url               HA API root, e.g. http://localhost:8123/api/.
  TOKENVAR          Name of the env var holding the long-lived token.
  -n, --namespace   RDF namespace for individuals.
  -o, --out         Output Turtle filename.
  -p, --privacy     Enable privacy filter.
  -m, --mount       ForcedIPHTTPSAdapter override IP for internal HA URLs.
  -c, --certificate Path to CA cert; "None" disables validation.
```

## Tests

Offline, no live Home Assistant and no token required:

```bash
python -m pytest tools/hass-to-rdt/tests -q
```
