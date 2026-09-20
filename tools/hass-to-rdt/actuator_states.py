"""
actuator_states.py — Domain-specific actuator state extractors
==============================================================
Pure-Python helpers with no dependency on the ``homeassistant`` package.
Each extractor receives:
  - ``attrs``       : dict of entity attributes returned by HA's /api/states
  - ``service_info``: dict of service definitions for the domain (may be {})
and returns a list of state strings to emit as ``rdt:hasActuatorState`` literals.

Imported by hacvt_rdt.py and tested independently in tests/test_actuator_states.py.
"""

import sys


def extract_fan_states(attrs: dict, service_info: dict) -> list[str]:
    """
    Priority: preset_modes > speed_list > percentage_step range.

    If both preset_modes and speed_list are present the function returns both
    (preset_modes first, unique speed_list values appended) and prints a
    warning to stderr so the ontology engineer can decide which source to trust.
    When neither list is available a discrete percentage scale is built from
    percentage_step (e.g. step=25 → pct:0, pct:25, pct:50, pct:75, pct:100).
    """
    preset_modes = attrs.get("preset_modes") or []
    speed_list   = attrs.get("speed_list") or []
    pct_step     = attrs.get("percentage_step")

    states: list[str] = []

    if preset_modes:
        states.extend(str(m) for m in preset_modes)

    if speed_list:
        if preset_modes:
            print(
                "WARNING: fan entity exposes both preset_modes and speed_list"
                f" — emitting both (preset_modes={list(preset_modes)},"
                f" speed_list={list(speed_list)})",
                file=sys.stderr,
            )
        states.extend(str(s) for s in speed_list if str(s) not in states)

    if not states and pct_step is not None:
        try:
            step = float(pct_step)
            if 0 < step <= 100:
                pct = 0.0
                while pct <= 100.0:
                    states.append(f"pct:{int(pct)}")
                    pct += step
        except (TypeError, ValueError):
            pass

    return states


def extract_climate_states(attrs: dict, service_info: dict) -> list[str]:
    """
    Concatenates four HA climate attribute lists, prefixing sub-lists to
    avoid value collisions:
      - hvac_modes   → bare strings (e.g. "heat", "cool", "off")
      - preset_modes → "preset:<value>"
      - fan_modes    → "fan:<value>"
      - swing_modes  → "swing:<value>"
    """
    states: list[str] = []

    for mode in attrs.get("hvac_modes") or []:
        states.append(str(mode))

    for mode in attrs.get("preset_modes") or []:
        states.append(f"preset:{mode}")

    for mode in attrs.get("fan_modes") or []:
        states.append(f"fan:{mode}")

    for mode in attrs.get("swing_modes") or []:
        states.append(f"swing:{mode}")

    return states


def extract_cover_states(attrs: dict, service_info: dict) -> list[str]:
    """
    If the entity reports a tilt position (``current_tilt_position`` key is
    present in attrs, regardless of value), return three positions:
    open, half-open, closed.  Otherwise return the two standard positions:
    open, closed.

    Note: the toggle states open/closed/stop are already emitted by
    _TOGGLE_OVERRIDE in hacvt_rdt.py; deduplication happens there.
    """
    if "current_tilt_position" in attrs:
        return ["open", "half-open", "closed"]
    return ["open", "closed"]


def extract_input_select_states(attrs: dict, service_info: dict) -> list[str]:
    """Return the ``options`` list verbatim, coercing each item to str."""
    return [str(o) for o in (attrs.get("options") or [])]
