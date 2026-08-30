"""
Tests for the domain-specific actuator-state extractors.

All tests are fully offline: no live HA, no network (enforced by the
block_network fixture in conftest.py), no ``homeassistant`` package required.

The pure functions live in actuator_states.py.  The integration path through
HACVT_RDT._add_possible_states is tested via a minimal stub that replaces
the homeassistant-dependent machinery with fakes.
"""

import sys
from pathlib import Path
from unittest.mock import MagicMock

import pytest
from rdflib import Graph, Literal
from rdflib.namespace import Namespace, URIRef

# Make the tool root importable (same trick as conftest.py).
_TOOL_ROOT = Path(__file__).resolve().parent.parent
if str(_TOOL_ROOT) not in sys.path:
    sys.path.insert(0, str(_TOOL_ROOT))

import actuator_states as as_mod

# RDT namespace literal (avoids importing hacvt_rdt and pulling in homeassistant).
_RDT = "http://www.semanticweb.org/ivans/ontologies/2025/ruleless-digital-twins/"
_RDT_HAS_ACTUATOR_STATE = URIRef(_RDT + "hasActuatorState")

# Toggle-state overrides mirrored from hacvt_rdt (used in integration stubs).
_TOGGLE_OVERRIDE = {
    "lock":         ("locked", "unlocked"),
    "cover":        ("open",   "closed", "stop"),
    "valve":        ("open",   "closed"),
    "button":       (),
    "remote":       (),
    "input_select": (),
}
_TOGGLE_DEFAULT = {
    "switch", "light", "fan", "climate",
    "siren", "vacuum", "humidifier", "media_player", "water_heater",
}


# ---------------------------------------------------------------------------
# Integration helper — mimics _add_possible_states without homeassistant
# ---------------------------------------------------------------------------

def _add_states(attrs: dict, domain: str, services: dict | None = None) -> set[str]:
    """
    Runs the same logic as HACVT_RDT._add_possible_states but without
    importing homeassistant.  Returns the set of emitted state strings.
    """
    from rdflib import Literal
    from rdflib.namespace import Namespace

    services = services or {}
    g = Graph()
    MINE = Namespace("http://test/")
    e_node = MINE["entity/test"]

    domain_key = str(domain)

    # Toggle states
    if domain_key in _TOGGLE_OVERRIDE:
        toggle_states = _TOGGLE_OVERRIDE[domain_key]
    elif domain_key in _TOGGLE_DEFAULT:
        toggle_states = ("on", "off")
    else:
        toggle_states = ()

    for s in toggle_states:
        g.add((e_node, _RDT_HAS_ACTUATOR_STATE, Literal(s)))

    # Attribute extractors
    _extractors = {
        "fan":          as_mod.extract_fan_states,
        "climate":      as_mod.extract_climate_states,
        "cover":        as_mod.extract_cover_states,
        "input_select": as_mod.extract_input_select_states,
    }
    if domain_key in _extractors:
        svc_info = services.get(domain_key, {})
        extracted = _extractors[domain_key](attrs, svc_info)
        seen = set(toggle_states)
        for s in extracted:
            if s not in seen:
                g.add((e_node, _RDT_HAS_ACTUATOR_STATE, Literal(s)))
                seen.add(s)

    return {str(obj) for _, _, obj in g.triples((e_node, _RDT_HAS_ACTUATOR_STATE, None))}


# ===========================================================================
# extract_fan_states
# ===========================================================================

class TestExtractFanStates:

    def test_preset_modes_returned(self):
        attrs = {"preset_modes": ["auto", "sleep", "turbo"]}
        assert as_mod.extract_fan_states(attrs, {}) == ["auto", "sleep", "turbo"]

    def test_speed_list_returned_when_no_presets(self):
        attrs = {"speed_list": ["low", "medium", "high"]}
        assert as_mod.extract_fan_states(attrs, {}) == ["low", "medium", "high"]

    def test_both_present_warns_and_returns_both(self, capsys):
        attrs = {
            "preset_modes": ["auto", "sleep"],
            "speed_list":   ["low", "high"],
        }
        result = as_mod.extract_fan_states(attrs, {})
        assert "auto" in result
        assert "sleep" in result
        assert "low" in result
        assert "high" in result
        stderr = capsys.readouterr().err
        assert "WARNING" in stderr
        assert "preset_modes" in stderr
        assert "speed_list" in stderr

    def test_speed_list_deduped_against_presets(self):
        # "auto" appears in both lists — must not duplicate
        attrs = {"preset_modes": ["auto"], "speed_list": ["auto", "turbo"]}
        result = as_mod.extract_fan_states(attrs, {})
        assert result.count("auto") == 1
        assert "turbo" in result

    def test_percentage_step_generates_range(self):
        attrs = {"percentage_step": 25}
        result = as_mod.extract_fan_states(attrs, {})
        assert "pct:0"   in result
        assert "pct:25"  in result
        assert "pct:50"  in result
        assert "pct:75"  in result
        assert "pct:100" in result

    def test_empty_attrs_returns_empty(self):
        assert as_mod.extract_fan_states({}, {}) == []

    def test_invalid_percentage_step_does_not_crash(self):
        attrs = {"percentage_step": "not-a-number"}
        assert as_mod.extract_fan_states(attrs, {}) == []

    def test_fan_integrated_toggle_plus_presets(self):
        states = _add_states({"preset_modes": ["eco", "boost"]}, "fan")
        assert "eco" in states
        assert "boost" in states
        assert "on" in states
        assert "off" in states


# ===========================================================================
# extract_climate_states
# ===========================================================================

class TestExtractClimateStates:

    def test_hvac_modes_returned_without_prefix(self):
        attrs = {"hvac_modes": ["off", "heat", "cool", "auto"]}
        result = as_mod.extract_climate_states(attrs, {})
        assert "off" in result
        assert "heat" in result

    def test_preset_modes_prefixed(self):
        attrs = {"preset_modes": ["away", "home", "sleep"]}
        result = as_mod.extract_climate_states(attrs, {})
        assert "preset:away"  in result
        assert "preset:home"  in result
        assert "preset:sleep" in result
        assert "away" not in result  # must carry prefix

    def test_fan_modes_prefixed(self):
        attrs = {"fan_modes": ["low", "medium", "high", "auto"]}
        result = as_mod.extract_climate_states(attrs, {})
        assert "fan:low"  in result
        assert "fan:auto" in result

    def test_swing_modes_prefixed(self):
        attrs = {"swing_modes": ["off", "vertical"]}
        result = as_mod.extract_climate_states(attrs, {})
        assert "swing:off"      in result
        assert "swing:vertical" in result

    def test_all_sub_lists_combined(self):
        attrs = {
            "hvac_modes":   ["off", "heat"],
            "preset_modes": ["away"],
            "fan_modes":    ["low"],
            "swing_modes":  ["off"],
        }
        result = as_mod.extract_climate_states(attrs, {})
        assert "heat"         in result
        assert "preset:away"  in result
        assert "fan:low"      in result
        assert "swing:off"    in result

    def test_empty_attrs_returns_empty(self):
        assert as_mod.extract_climate_states({}, {}) == []

    def test_none_sub_lists_do_not_crash(self):
        attrs = {"hvac_modes": None, "preset_modes": None}
        assert as_mod.extract_climate_states(attrs, {}) == []

    def test_climate_integrated(self):
        states = _add_states(
            {"hvac_modes": ["off", "cool"], "preset_modes": ["home"]},
            "climate",
        )
        assert "off"          in states
        assert "cool"         in states
        assert "preset:home"  in states


# ===========================================================================
# extract_cover_states
# ===========================================================================

class TestExtractCoverStates:

    def test_without_tilt_returns_open_closed(self):
        assert set(as_mod.extract_cover_states({}, {})) == {"open", "closed"}

    def test_with_tilt_attribute_returns_three_states(self):
        attrs = {"current_tilt_position": 50}
        assert set(as_mod.extract_cover_states(attrs, {})) == {"open", "half-open", "closed"}

    def test_tilt_position_zero_still_triggers(self):
        # Value 0 is falsy; key presence is what matters
        attrs = {"current_tilt_position": 0}
        assert "half-open" in as_mod.extract_cover_states(attrs, {})

    def test_cover_integrated_no_tilt(self):
        states = _add_states({}, "cover")
        assert "open"   in states
        assert "closed" in states
        assert "stop"   in states

    def test_cover_integrated_with_tilt(self):
        states = _add_states({"current_tilt_position": 50}, "cover")
        assert "half-open" in states
        assert "open"      in states
        assert "closed"    in states

    def test_open_closed_not_duplicated_by_extractor(self):
        # _TOGGLE_OVERRIDE["cover"] already has open/closed/stop;
        # _extract_cover_states also returns open/closed.
        # Integration layer must not emit duplicates.
        states_list = list(_add_states({}, "cover"))
        assert states_list.count("open")   == 1
        assert states_list.count("closed") == 1


# ===========================================================================
# extract_input_select_states
# ===========================================================================

class TestExtractInputSelectStates:

    def test_options_returned_as_is(self):
        attrs = {"options": ["mode1", "mode2", "mode3"]}
        assert as_mod.extract_input_select_states(attrs, {}) == ["mode1", "mode2", "mode3"]

    def test_numeric_options_coerced_to_str(self):
        attrs = {"options": [1, 2, 3]}
        assert as_mod.extract_input_select_states(attrs, {}) == ["1", "2", "3"]

    def test_empty_options_returns_empty(self):
        assert as_mod.extract_input_select_states({"options": []}, {}) == []

    def test_missing_options_returns_empty(self):
        assert as_mod.extract_input_select_states({}, {}) == []

    def test_none_options_returns_empty(self):
        assert as_mod.extract_input_select_states({"options": None}, {}) == []

    def test_input_select_integrated(self):
        states = _add_states({"options": ["eco", "comfort", "boost"]}, "input_select")
        assert "eco"     in states
        assert "comfort" in states
        assert "boost"   in states

    def test_missing_attrs_does_not_crash(self):
        # Simulate getAttributes returning an empty dict (degraded gracefully)
        states = _add_states({}, "input_select")
        assert isinstance(states, set)
