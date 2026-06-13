"""
hacvt_rdt.py — SOSA/RDT exporter for Home Assistant
=====================================================
Inherits from HACVT (hacvt.py) and overrides the ontology backend
to produce .ttl files compatible with the RDT SmartNode ontology:
  http://www.semanticweb.org/ivans/ontologies/2025/ruleless-digital-twins/

Key differences from hacvt.py (SAREF backend):
  - Uses SOSA (http://www.w3.org/ns/sosa/) and SSN namespaces
  - Maps HA domains → sosa:Sensor / sosa:Actuator / sosa:Platform
  - Attaches rdt:hasIdentifier with the raw HA entity_id
  - Queries /api/services to capture possible actuator states
    and emits rdt:hasActuatorState triples for each
  - ObservableProperty used for sensor measurement targets
  - No SAREF, S4BLDG or homeassistantcore.rdf side-effect

Usage (same CLI as hacvt.py):
  python hacvt_rdt.py http://homeassistant.local:8123/api/ HA_TOKEN \\
      --namespace "http://www.semanticweb.org/rayan/ontologies/2025/ha/" \\
      --out ha_rdt.ttl
"""

import argparse
import logging
from typing import Optional

from rdflib import Graph, Literal, URIRef
from rdflib.namespace import Namespace, RDF, RDFS, OWL, XSD

import homeassistant.const as hc
import homeassistant.core as ha

from ConfigSource import CLISource
from hacvt import HACVT, PrivacyFilter, mkname
from actuator_states import (
    extract_fan_states,
    extract_climate_states,
    extract_cover_states,
    extract_input_select_states,
)


# ---------------------------------------------------------------------------
# Namespaces
# ---------------------------------------------------------------------------
SOSA_NS = Namespace("http://www.w3.org/ns/sosa/")
SSN_NS  = Namespace("http://www.w3.org/ns/ssn/")
RDT_NS  = Namespace(
    "http://www.semanticweb.org/ivans/ontologies/2025/ruleless-digital-twins/"
)


# ---------------------------------------------------------------------------
# Domain → SOSA class mapping
# ---------------------------------------------------------------------------
# True  → use HA sub-class  (HASS[domain.title()] rdfs:subClassOf sosa:X)
# False → use sosa:X directly
# None  → skip this domain entirely
_DOMAIN_TO_SOSA: dict = {
    hc.Platform.BINARY_SENSOR:       (False, "Sensor"),
    hc.Platform.SENSOR:              (False, "Sensor"),
    hc.Platform.AIR_QUALITY:         (True,  "Sensor"),
    hc.Platform.DEVICE_TRACKER:      (True,  "Sensor"),
    hc.Platform.WEATHER:             (True,  "Sensor"),
    hc.Platform.SWITCH:              (True,  "Actuator"),
    hc.Platform.FAN:                 (True,  "Actuator"),
    hc.Platform.LIGHT:               (True,  "Actuator"),
    hc.Platform.COVER:               (True,  "Actuator"),
    hc.Platform.LOCK:                (True,  "Actuator"),
    hc.Platform.HUMIDIFIER:          (True,  "Actuator"),
    hc.Platform.SIREN:               (True,  "Actuator"),
    hc.Platform.VACUUM:              (True,  "Actuator"),
    hc.Platform.WATER_HEATER:        (True,  "Actuator"),
    hc.Platform.CLIMATE:             (True,  "Actuator"),
    hc.Platform.MEDIA_PLAYER:        (True,  "Actuator"),
    hc.Platform.BUTTON:              (True,  "Actuator"),
    hc.Platform.REMOTE:              (True,  "Actuator"),
    # hc.Platform.VALVE was added in HA 2024+. Older HA versions (e.g. 2023.7.3
    # pinned in the internship venv) raise AttributeError on direct access.
    # Use getattr-on-class with a fallback sentinel string to stay forward-
    # compatible without crashing. The sentinel will never collide with a real
    # HA domain string.
    (getattr(hc.Platform, "VALVE", "_internship_skip_valve_")): (True, "Actuator"),
    hc.Platform.CAMERA:              (True,  "Platform"),
    hc.Platform.ALARM_CONTROL_PANEL: (True,  "Platform"),
    # HA helper domains (not in hc.Platform enum — string literals required)
    "input_number":  (False, "Sensor"),   # virtual numeric sensors
    "input_boolean": (False, "Sensor"),   # observable boolean states
    "input_select":  (True,  "Actuator"), # mode selectors (set via select_option)
    # Skipped domains
    hc.Platform.CALENDAR:         None,
    hc.Platform.GEO_LOCATION:     None,
    hc.Platform.IMAGE_PROCESSING: None,
    hc.Platform.NOTIFY:           None,
    hc.Platform.NUMBER:           None,
    hc.Platform.SCENE:            None,
    hc.Platform.SELECT:           None,
    hc.Platform.STT:              None,
    hc.Platform.TEXT:             None,
    hc.Platform.TTS:              None,
    hc.Platform.UPDATE:           None,
}

_ACTUATOR_DOMAINS = {
    p for p, v in _DOMAIN_TO_SOSA.items()
    if v is not None and v[1] == "Actuator"
}

# ---------------------------------------------------------------------------
# Bug #2 fix — per-domain toggle state overrides
# ---------------------------------------------------------------------------
# Maps domain string → tuple of default states to emit (replaces "on"/"off")
# Empty tuple () means: no toggle states at all for this domain
_TOGGLE_OVERRIDE: dict = {
    "lock":    ("locked", "unlocked"),
    "cover":   ("open",   "closed", "stop"),
    "valve":        ("open",   "closed"),
    "button":       (),  # button has no meaningful toggle states
    "remote":       (),  # remote uses service calls, not toggle states
    "input_select": (),  # options are entity-specific; harvested from /api/services
}

# Domains that get "on"/"off" by default (unless overridden above)
_TOGGLE_DEFAULT = {
    "switch", "light", "fan", "climate",
    "siren", "vacuum", "humidifier", "media_player",
    "water_heater",
}

# ---------------------------------------------------------------------------
# Actuation semantics — what each actuator domain AFFECTS.
# ---------------------------------------------------------------------------
# The HA API tells us a light exists and toggles on/off, but never that a
# light affects "illuminance". That mapping is domain knowledge. We encode a
# reasonable default per domain so the exporter can auto-generate the
# actuator -> PropertyChange -> Property links the MAPE-K planner needs,
# instead of requiring a hand-written overlay. The generated overlay is still
# an editable ontological instance file: users can refine it via --enrich.
_DOMAIN_TO_AFFECTED_PROPERTY: dict = {
    "light":         "illuminance",
    "switch":        "switch_state",
    "fan":           "air_flow",
    "climate":       "temperature",
    "cover":         "position",
    "lock":          "lock_state",
    "media_player":  "playback_state",
    "humidifier":    "humidity",
    "siren":         "siren_state",
    "vacuum":        "vacuum_state",
    "water_heater":  "water_temperature",
    "button":        "button_state",
    "remote":        "remote_state",
    "input_boolean": "boolean_state",
    "input_select":  "selected_mode",
    "valve":         "valve_position",
}
# Number of discrete steps generated per continuous lever (ConfigurableParameter).
_CONFIG_PARAM_STEPS = 3  # min / mid / max

# When picking one lever per actuator, prefer meaningful fields (a light's
# brightness) over incidental ones (transition duration, flash). Fields not
# listed keep their natural order, after the prioritised ones.
_LEVER_FIELD_PRIORITY = [
    "brightness", "brightness_pct", "percentage", "percentage_step",
    "temperature", "target_temp_high", "target_temp_low", "target_temperature",
    "position", "tilt_position", "volume_level", "color_temp", "humidity",
]


class HACVT_RDT(HACVT):
    """
    SOSA/RDT variant of HACVT.

    Overrides:
      - main()                  : sets up SOSA graph instead of SAREF
      - _handle_entity_rdt()    : maps to sosa:Sensor / sosa:Actuator
      - _add_possible_states()  : harvests /api/services for actuator states
    """

    # ------------------------------------------------------------------
    # Entry point
    # ------------------------------------------------------------------
    def main(
        self,
        debug=logging.INFO,
        certificate=None,
        privacy=None,
        namespace="http://my.name.space/ha/",
        max_levers=0,
    ):
        # Max number of continuous levers (ConfigurableParameters) to include,
        # at most one per actuator. 0 keeps the clean on/off tree; raising it
        # adds dimmer-style control but grows the simulation tree fast
        # (each lever multiplies the combinations by _CONFIG_PARAM_STEPS).
        self._max_levers = max_levers
        logging.basicConfig(level=debug, format="%(levelname)s: %(message)s")
        self.cs.ws = self.cs._ws_connect(certificate=certificate)
        self.cs.ws_counter = 1

        pf = PrivacyFilter(self.cs)
        pf.privacyFilter_init(privacy=privacy)

        g = Graph(bind_namespaces="core")
        MINE, HASS = self._setup_sosa(g, namespace)

        # Collectors for auto-generated actuation semantics.
        self._rdt_actuators = []     # list of (e_node, domain)
        self._rdt_configparams = []  # list of (e_node, field, v_min, v_max)

        the_devices = self.cs.getDevices()
        for d in the_devices:
            d_g          = pf.mkDevice(MINE, d)
            manufacturer = self.cs.getDeviceAttr(d, hc.ATTR_MANUFACTURER)
            name         = self.cs.getDeviceAttr(d, hc.ATTR_NAME)
            model        = self.cs.getDeviceAttr(d, hc.ATTR_MODEL)

            # ----------------------------------------------------------
            # Bug #1 fix — avoid mine:device/None_None superclass
            # Only create a named sub-class when both manufacturer AND
            # model are real values; otherwise type directly as Platform.
            # ----------------------------------------------------------
            mfr_valid = manufacturer and str(manufacturer) not in ("None", "")
            mdl_valid = model        and str(model)        not in ("None", "")
            if mfr_valid and mdl_valid:
                d_super = MINE["device/" + mkname(manufacturer) + "_" + mkname(model)]
                g.add((d_super, RDFS.subClassOf, SOSA_NS["Platform"]))
                g.add((d_g,     RDF.type,         d_super))
            else:
                g.add((d_g, RDF.type, SOSA_NS["Platform"]))

            g.add((d_g, RDFS.label, Literal(name)))

            # Area
            d_area    = self.cs.getYAMLText(f'area_id("{d}")')
            area_name = self.cs.getYAMLText(f'area_name("{d}")')
            if d_area.strip() not in ("None", ""):
                area = pf.mkLocationURI(MINE, d_area.strip())
                g.add((area,  RDF.type,            SOSA_NS["Platform"]))
                g.add((area,  RDFS.label,           Literal(area_name.strip())))
                g.add((d_g,   SSN_NS["isHostedBy"], area))

            # Entities hosted by this device
            es = self.cs.getDeviceEntities(d)
            for e in es:
                if not (isinstance(e, str) and e.count(".") == 1):
                    continue
                e_node = self._handle_entity_rdt(pf, MINE, HASS, d, e, g)
                if e_node is not None:
                    g.add((d_g, SOSA_NS["hosts"], e_node))

        # Entities without a parent device
        for e_state in self._get_entities_wo_device():
            e_id = e_state["entity_id"]
            if e_id.count(".") != 1:
                continue
            self._handle_entity_rdt(pf, MINE, HASS, None, e_id, g)

        # Auto-generate the actuation model (system platform, actuator ->
        # PropertyChange -> Property links, and ConfigurableParameters).
        self._emit_rdt_actuation(g, MINE)

        logging.info("Serialising RDT/SOSA graph ...")
        return g

    # ------------------------------------------------------------------
    # Auto-generated actuation semantics
    # ------------------------------------------------------------------
    def _emit_rdt_actuation(self, g: Graph, MINE: Namespace):
        """Emit, from the collected actuators/config-params, everything the
        MAPE-K planner needs to enumerate a simulation tree:
          - a single system sosa:Platform carrying the enumeration flag and
            hosting every actuator;
          - per actuator: an ObservableProperty + a PropertyChangeByActuation
            (ValueIncrease) + rdt:enacts;
          - per continuous lever: a ConfigurableParameter with discrete
            possible values + increase/decrease PropertyChanges.
        All of this is instance-level semantics; the generic engine is
        untouched."""
        actuators = getattr(self, "_rdt_actuators", [])
        configs   = getattr(self, "_rdt_configparams", [])
        if not actuators and not configs:
            return

        # 1. System platform with the full-enumeration flag, hosting actuators.
        system = MINE["ha_system"]
        g.add((system, RDF.type, OWL.NamedIndividual))
        g.add((system, RDF.type, SOSA_NS["Platform"]))
        g.add((system, RDT_NS["generateCombinationsOnlyFromOptimalConditions"],
               Literal(False)))

        # 2. Per-actuator actuation link.
        for e_node, domain in actuators:
            g.add((system, SOSA_NS["hosts"], e_node))
            prop_name = _DOMAIN_TO_AFFECTED_PROPERTY.get(domain, "state")
            local     = mkname(str(e_node).rsplit("/", 1)[-1])
            prop      = MINE[f"prop/{local}_{prop_name}"]
            change    = MINE[f"change/{local}"]
            g.add((prop, RDF.type, OWL.NamedIndividual))
            g.add((prop, RDF.type, SOSA_NS["ObservableProperty"]))
            g.add((prop, RDT_NS["hasValue"], Literal(0.0)))
            g.add((change, RDF.type, OWL.NamedIndividual))
            g.add((change, RDF.type, RDT_NS["PropertyChangeByActuation"]))
            g.add((change, SSN_NS["forProperty"], prop))
            g.add((change, RDT_NS["affectsPropertyWith"], RDT_NS["ValueIncrease"]))
            g.add((e_node, RDT_NS["enacts"], change))
            g.add((e_node, RDT_NS["hasActuatorName"], Literal(f"{local}_state")))

        # 3. Per continuous lever: a ConfigurableParameter with N discrete
        #    possible values and increase/decrease PropertyChanges.
        #    Capped to at most one lever per actuator and `_max_levers` total,
        #    to keep the simulation tree from exploding (each lever multiplies
        #    the combination count by _CONFIG_PARAM_STEPS).
        max_levers = getattr(self, "_max_levers", 0)
        selected, seen_actuators = [], set()
        if max_levers > 0:
            # Prefer meaningful fields (brightness) over incidental ones
            # (transition). Stable sort: prioritised fields first, rest as-is.
            def _prio(entry):
                field = entry[1]
                return _LEVER_FIELD_PRIORITY.index(field) if field in _LEVER_FIELD_PRIORITY else len(_LEVER_FIELD_PRIORITY)
            for entry in sorted(configs, key=_prio):
                e_node = entry[0]
                if e_node in seen_actuators:
                    continue            # one lever per actuator
                seen_actuators.add(e_node)
                selected.append(entry)
                if len(selected) >= max_levers:
                    break
        configs = selected

        for e_node, field, v_min, v_max, entity_id, domain in configs:
            g.add((system, SOSA_NS["hosts"], e_node))
            local     = mkname(str(e_node).rsplit("/", 1)[-1])
            fname     = mkname(field)
            prop      = MINE[f"prop/{local}_{fname}"]
            param     = MINE[f"param/{local}_{fname}"]
            inc       = MINE[f"change/{local}_{fname}_inc"]
            dec       = MINE[f"change/{local}_{fname}_dec"]
            g.add((prop, RDF.type, OWL.NamedIndividual))
            g.add((prop, RDF.type, SOSA_NS["ObservableProperty"]))
            g.add((prop, RDT_NS["hasValue"], Literal(0.0)))
            for ch, direction in ((inc, "ValueIncrease"), (dec, "ValueDecrease")):
                g.add((ch, RDF.type, OWL.NamedIndividual))
                g.add((ch, RDF.type, RDT_NS["PropertyChangeByActuation"]))
                g.add((ch, SSN_NS["forProperty"], prop))
                g.add((ch, RDT_NS["affectsPropertyWith"], RDT_NS[direction]))
            g.add((param, RDF.type, OWL.NamedIndividual))
            g.add((param, RDF.type, RDT_NS["ConfigurableParameter"]))
            g.add((param, RDT_NS["enacts"], inc))
            g.add((param, RDT_NS["enacts"], dec))
            # Runtime binding: which HA entity + service field this lever drives,
            # so HaConfigurableParameter can POST the chosen value back to HA.
            g.add((param, RDT_NS["hasIdentifier"], Literal(entity_id, datatype=XSD.string)))
            g.add((param, RDT_NS["hasActuatorName"], Literal(field, datatype=XSD.string)))
            # N discrete possible values (min / mid / max for N=3).
            levels = self._discrete_levels(v_min, v_max, _CONFIG_PARAM_STEPS)
            for val in levels:
                g.add((param, RDT_NS["hasPossibleValue"],
                       Literal(val, datatype=XSD.double)))
            # Current value — the MAPE-K Monitor reads `meta:hasValue` to seed
            # the parameter in its cache. Without it the planner throws a
            # KeyNotFoundException when building ReconfigurationActions. Seed it
            # with the first (minimum) level.
            if levels:
                g.add((param, RDT_NS["hasValue"],
                       Literal(levels[0], datatype=XSD.double)))

        n_act = len(actuators)
        n_cfg = len(configs)
        logging.info(
            f"Auto-generated actuation: 1 system platform, {n_act} actuator "
            f"link(s), {n_cfg} configurable parameter(s).")

    @staticmethod
    def _discrete_levels(v_min, v_max, steps):
        """Return `steps` evenly-spaced values from v_min to v_max inclusive."""
        try:
            lo = float(v_min)
            hi = float(v_max)
        except (TypeError, ValueError):
            return []
        if steps <= 1 or hi <= lo:
            return [lo]
        span = (hi - lo) / (steps - 1)
        return [round(lo + span * i, 4) for i in range(steps)]

    # ------------------------------------------------------------------
    # Graph / namespace setup
    # ------------------------------------------------------------------
    def _setup_sosa(self, g: Graph, namespace: str):
        MINE = Namespace(namespace)
        HASS = Namespace("https://www.foldr.org/profiles/homeassistant/")

        g.bind("sosa",  SOSA_NS)
        g.bind("ssn",   SSN_NS)
        g.bind("rdt",   RDT_NS)
        g.bind("hass",  HASS)
        g.bind("mine",  MINE)
        g.bind("owl",   OWL)

        ont = URIRef(str(MINE))
        g.add((ont, RDF.type,    OWL.Ontology))
        g.add((ont, OWL.imports, URIRef("http://www.w3.org/ns/ssn/")))
        g.add((ont, OWL.imports, URIRef("http://www.w3.org/ns/sosa/")))

        # Declare RDT datatype properties so the file is self-contained
        for prop in ("hasIdentifier", "hasActuatorState", "hasPossibleValue"):
            g.add((RDT_NS[prop], RDF.type, OWL.DatatypeProperty))

        # Sub-class HA domains under the appropriate SOSA class
        for domain, mapping in _DOMAIN_TO_SOSA.items():
            if mapping is None:
                continue
            subclass_flag, sosa_class = mapping
            if subclass_flag:
                g.add((HASS[domain.title()], RDFS.subClassOf, SOSA_NS[sosa_class]))

        return MINE, HASS

    # ------------------------------------------------------------------
    # Entity handler — SOSA version
    # ------------------------------------------------------------------
    def _handle_entity_rdt(
        self,
        pf: PrivacyFilter,
        MINE: Namespace,
        HASS: Namespace,
        device: Optional[str],
        entity_id: str,
        g: Graph,
    ) -> Optional[URIRef]:

        domain, _ = ha.split_entity_id(entity_id)
        mapping   = _DOMAIN_TO_SOSA.get(domain)

        if mapping is None:
            logging.warning(f"Skipping {entity_id}: domain '{domain}' not mapped.")
            return None

        subclass_flag, sosa_class = mapping
        sosa_type = HASS[domain.title()] if subclass_flag else SOSA_NS[sosa_class]

        e_node, e_name = pf.mkEntityURI(MINE, entity_id)
        g.add((e_node, RDF.type,               sosa_type))
        # Also assert the direct SOSA type (sosa:Sensor / sosa:Actuator /
        # sosa:Platform). The RDT inference engine does not propagate
        # rdfs:subClassOf, and the MAPE-K SPARQL queries match the direct
        # type (e.g. `?actuator a sosa:Actuator`). Without this, actuators
        # typed only via a domain subclass (a hass:Light) are invisible to
        # the planner and the simulation tree comes out empty.
        if subclass_flag:
            g.add((e_node, RDF.type, SOSA_NS[sosa_class]))
        g.add((e_node, RDT_NS["hasIdentifier"], Literal(entity_id, datatype=XSD.string)))

        # Friendly name
        try:
            attrs = self.cs.getAttributes(entity_id)
            fname = attrs.get(hc.ATTR_FRIENDLY_NAME)
            if fname:
                g.add((e_node, RDFS.label, Literal(fname)))
        except Exception:
            pass

        # Sensors: link to an ObservableProperty + emit a Procedure
        if sosa_class == "Sensor":
            prop_node = MINE["property/" + mkname(e_name)]
            g.add((prop_node, RDF.type,           SOSA_NS["ObservableProperty"]))
            g.add((e_node,    SOSA_NS["observes"], prop_node))

            proc_node = MINE["procedure/" + mkname(e_name)]
            g.add((proc_node, RDF.type,            SOSA_NS["Procedure"]))
            g.add((e_node,    SSN_NS["implements"], proc_node))
            # Mirror the ObservableProperty on the Procedure (consistent with
            # upstream instance-model-1.ttl which links Procedure → inputs).
            g.add((proc_node, SOSA_NS["observes"], prop_node))

        # Actuators: harvest possible states; only attach Procedure when useful
        elif sosa_class == "Actuator":
            # Record this actuator so _emit_rdt_actuation can wire its
            # PropertyChange/system-platform links after all entities are seen.
            if hasattr(self, "_rdt_actuators"):
                self._rdt_actuators.append((e_node, domain))
            states_added = self._add_possible_states(g, MINE, e_node, entity_id, domain)
            # Bug #3 fix — only create sosa:Procedure when the actuator
            # actually has states to expose (button/remote have none).
            if states_added > 0:
                proc_node = MINE["procedure/" + mkname(e_name)]
                g.add((proc_node, RDF.type,             SOSA_NS["Procedure"]))
                g.add((e_node,    SOSA_NS["implements"], proc_node))

        return e_node

    # ------------------------------------------------------------------
    # Domain-specific state extractors — thin aliases to actuator_states.py
    # (kept as static methods so existing call-sites stay unchanged).
    # ------------------------------------------------------------------
    _extract_fan_states          = staticmethod(extract_fan_states)
    _extract_climate_states      = staticmethod(extract_climate_states)
    _extract_cover_states        = staticmethod(extract_cover_states)
    _extract_input_select_states = staticmethod(extract_input_select_states)

    # ------------------------------------------------------------------
    # Harvest possible actuator states from /api/services and entity attrs.
    # Returns the number of rdt:hasActuatorState triples added.
    # ------------------------------------------------------------------
    def _add_possible_states(
        self,
        g: Graph,
        MINE: Namespace,
        e_node: URIRef,
        entity_id: str,
        domain,
    ) -> int:
        """
        Populate rdt:hasActuatorState triples.

        Strategy:
          1. Emit domain-specific toggle states (via _TOGGLE_OVERRIDE /
             _TOGGLE_DEFAULT) — Bug #2 fix: lock → locked/unlocked,
             cover → open/closed/stop, button/remote → nothing.
          2. Run domain-specific attribute extractor if one exists
             (fan, climate, cover, input_select).
          3. Inspect /api/services service fields:
             - selector.select  → explicit enum values
             - selector.number  → range annotation string range:min:max:stepN
             - selector.boolean → "true" / "false"

        Returns the total count of triples added.
        """
        count      = 0
        services   = self.cs.getServices()
        domain_key = str(domain)

        # --- Bug #2 fix: per-domain toggle states ---
        if domain_key in _TOGGLE_OVERRIDE:
            toggle_states = _TOGGLE_OVERRIDE[domain_key]
        elif domain_key in _TOGGLE_DEFAULT:
            toggle_states = ("on", "off")
        else:
            toggle_states = ()

        for state in toggle_states:
            g.add((e_node, RDT_NS["hasActuatorState"], Literal(state)))
            count += 1

        # --- Domain-specific attribute extractors ---
        _attr_extractors = {
            "fan":          self._extract_fan_states,
            "climate":      self._extract_climate_states,
            "cover":        self._extract_cover_states,
            "input_select": self._extract_input_select_states,
        }
        if domain_key in _attr_extractors:
            try:
                attrs = self.cs.getAttributes(entity_id) or {}
            except Exception:
                attrs = {}
            svc_info = services.get(domain_key, {})
            extracted = _attr_extractors[domain_key](attrs, svc_info)
            # Deduplicate against already-emitted toggle states
            seen = set(toggle_states)
            for state in extracted:
                if state not in seen:
                    g.add((e_node, RDT_NS["hasActuatorState"], Literal(state)))
                    count += 1
                    seen.add(state)

        # --- /api/services selector fields ---
        if domain_key not in services:
            return count

        svc_map = services[domain_key]
        for svc_name, svc_info in svc_map.items():
            if not isinstance(svc_info, dict):
                continue
            fields = svc_info.get("fields", {})
            for field_name, field_info in fields.items():
                if not isinstance(field_info, dict):
                    continue
                selector = field_info.get("selector", {})
                if not isinstance(selector, dict):
                    continue

                # Explicit enum of possible values
                if "select" in selector:
                    options = selector["select"].get("options", [])
                    for opt in options:
                        val = opt if isinstance(opt, str) else opt.get("value", "")
                        if val:
                            g.add((e_node, RDT_NS["hasActuatorState"], Literal(val)))
                            count += 1

                # Numeric range (e.g. a light's brightness/color_temp number
                # field). A continuous range is NOT a discrete actuation state:
                # in the RDT model it is a ConfigurableParameter, not an
                # ActuatorState. We do NOT emit it as rdt:hasActuatorState
                # (that exploded the cartesian product); instead we record it
                # so _emit_rdt_actuation can model it as a ConfigurableParameter
                # with a small number of discrete possible values.
                if "number" in selector:
                    num_sel = selector["number"]
                    v_min   = num_sel.get("min")
                    v_max   = num_sel.get("max")
                    if v_min is not None and v_max is not None and hasattr(self, "_rdt_configparams"):
                        self._rdt_configparams.append(
                            (e_node, field_name, v_min, v_max, entity_id, domain))

                # Boolean field
                if "boolean" in selector:
                    for bv in ("true", "false"):
                        g.add((e_node, RDT_NS["hasActuatorState"], Literal(bv)))
                        count += 2

        return count

    # ------------------------------------------------------------------
    # Bug #4 fix — normalize getDeviceId() return value
    # getDeviceId() may return Python None OR the string "None"
    # ------------------------------------------------------------------
    def _get_entities_wo_device(self):
        for k in self.cs.getStates():
            raw = self.cs.getDeviceId(k["entity_id"])
            if not raw or str(raw).strip() in ("None", ""):
                yield k


# ---------------------------------------------------------------------------
# CLI
# ---------------------------------------------------------------------------
if __name__ == "__main__":
    parser = argparse.ArgumentParser(
        description="Export a Home Assistant instance to a SOSA/RDT-compatible .ttl file."
    )
    parser.add_argument(
        "-d", "--debug", default="INFO", const="DEBUG", nargs="?",
        help="Log level (INFO by default, DEBUG if flag given alone).",
    )
    parser.add_argument(
        "-n", "--namespace",
        default="http://www.semanticweb.org/rayan/ontologies/2025/ha/",
        help="Base namespace for generated individuals.",
    )
    parser.add_argument(
        "-o", "--out", default="ha_rdt.ttl",
        help="Output .ttl file (default: ha_rdt.ttl).",
    )
    parser.add_argument(
        "-p", "--privacy", nargs="*", metavar="platform",
        help="Enable privacy filter.",
    )
    parser.add_argument(
        "-e", "--enrich", action="append", default=None, metavar="overlay.ttl",
        help="Merge an extra instance-level overlay into the export (repeatable). "
             "The actuator->PropertyChange links and the system platform are now "
             "auto-generated, so this is only for additional hand-authored "
             "semantics.",
    )
    parser.add_argument(
        "-l", "--levers", type=int, default=0, metavar="N",
        help="Include up to N continuous levers (ConfigurableParameters, e.g. a "
             "light's brightness) with %d discrete steps each, at most one per "
             "actuator. Default 0 keeps the clean on/off tree; each lever "
             "multiplies the simulation-tree size." % _CONFIG_PARAM_STEPS,
    )

    cli  = CLISource(parser)
    tool = HACVT_RDT(cli)
    g    = tool.main(
        debug=cli.args.debug,
        certificate=cli.args.certificate,
        privacy=cli.args.privacy,
        namespace=cli.args.namespace,
        max_levers=cli.args.levers,
    )
    # Merge any instance-level overlays. Same namespaces -> triples fold in.
    for overlay in (cli.args.enrich or []):
        before = len(g)
        g.parse(overlay, format="turtle")
        logging.info(f"Merged overlay '{overlay}' (+{len(g) - before} triples).")
    with open(cli.args.out, "w") as f_out:
        f_out.write(g.serialize(format="turtle"))
    print(f"Written to {cli.args.out}")
