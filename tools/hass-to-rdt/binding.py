"""Pure helpers used by gen_bindings.py to build HA bindings JSON entries.

The shape produced here matches SmartNode/HaBindings/HaBindingsLoader.cs.
This module performs no I/O and no network calls.
"""

from __future__ import annotations

import re
from typing import Optional

# HA domain -> ActuatorKind enum value. Must stay in sync with
# Implementations/Actuators/HomeAssistant/HomeAssistantActuator.ActuatorKind.
_DOMAIN_TO_HA_KIND: dict[str, str] = {
    "light":         "Light",
    "switch":        "Switch",
    "input_boolean": "InputBoolean",
    "input_number":  "InputNumber",
    "input_select":  "InputSelect",
}

VALID_HA_KINDS = frozenset(_DOMAIN_TO_HA_KIND.values())

# Domains that exist in HA but are not driveable by the current SmartNode runtime.
KNOWN_UNSUPPORTED_DOMAINS = frozenset({"climate", "cover"})

_ENTITY_ID_RE = re.compile(r"^[a-z0-9_]+\.[a-z0-9_]+$")


def normalize_entity_id(entity_id: str) -> str:
    """Strip + lowercase, validate the ``domain.entity`` shape."""
    if entity_id is None:
        raise ValueError("entity_id is None")
    norm = entity_id.strip().lower()
    if not _ENTITY_ID_RE.match(norm):
        raise ValueError(f"invalid HA entity_id: {entity_id!r}")
    return norm


def infer_ha_kind(entity_id: str) -> Optional[str]:
    """Return the ActuatorKind for an entity_id or None if unsupported."""
    norm = normalize_entity_id(entity_id)
    domain = norm.split(".", 1)[0]
    return _DOMAIN_TO_HA_KIND.get(domain)


def make_sensor_binding(
    sensor_uri: str,
    entity_id: str,
    procedure_uri: Optional[str] = None,
) -> dict:
    """Build a HomeAssistant-kind sensor entry."""
    if not sensor_uri:
        raise ValueError("sensor_uri is empty")
    norm_id = normalize_entity_id(entity_id)
    proc = procedure_uri if procedure_uri else f"{sensor_uri}Procedure"
    return {
        "sensorUri": sensor_uri,
        "procedureUri": proc,
        "kind": "HomeAssistant",
        "haEntityId": norm_id,
    }


def make_actuator_binding(
    actuator_uri: str,
    entity_id: str,
    ha_kind: Optional[str] = None,
) -> dict:
    """Build a HomeAssistant-kind actuator entry.

    A ``ha_kind`` of None becomes JSON null. The reviewer must replace it
    before the file is renamed to ha-bindings.<profile>.json, otherwise
    HaBindingsLoader will throw at startup.
    """
    if not actuator_uri:
        raise ValueError("actuator_uri is empty")
    norm_id = normalize_entity_id(entity_id)
    if ha_kind is not None and ha_kind not in VALID_HA_KINDS:
        raise ValueError(
            f"ha_kind {ha_kind!r} is not a valid ActuatorKind "
            f"(expected one of {sorted(VALID_HA_KINDS)})"
        )
    return {
        "actuatorUri": actuator_uri,
        "kind": "HomeAssistant",
        "haEntityId": norm_id,
        "haKind": ha_kind,
    }


def validate_binding_shape(binding: dict) -> list[str]:
    """Return a list of human-readable shape errors. Empty list = OK.

    This is a best-effort sanity check, not a full mirror of
    HaBindingsLoader.cs validation rules.
    """
    if not isinstance(binding, dict):
        return [f"binding is not a dict: {type(binding).__name__}"]

    errors: list[str] = []

    if "sensorUri" in binding:
        if not binding.get("sensorUri"):
            errors.append("sensorUri is empty")
        if not binding.get("procedureUri"):
            errors.append("procedureUri is empty")
        kind = binding.get("kind")
        if kind == "HomeAssistant" and not binding.get("haEntityId"):
            errors.append("haEntityId required when kind=HomeAssistant")
        if kind in ("Constant", "GeneralConstant") and "constantValue" not in binding:
            errors.append(f"constantValue required when kind={kind}")
    elif "actuatorUri" in binding:
        if not binding.get("actuatorUri"):
            errors.append("actuatorUri is empty")
        kind = binding.get("kind")
        if kind == "HomeAssistant":
            if not binding.get("haEntityId"):
                errors.append("haEntityId required when kind=HomeAssistant")
            ha_kind = binding.get("haKind")
            if not ha_kind:
                errors.append("haKind required when kind=HomeAssistant")
            elif ha_kind not in VALID_HA_KINDS:
                errors.append(
                    f"haKind {ha_kind!r} not in {sorted(VALID_HA_KINDS)}"
                )
    else:
        errors.append("binding has neither sensorUri nor actuatorUri")

    return errors
