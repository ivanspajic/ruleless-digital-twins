"""Integration tests for tools/hass-to-rdt/gen_bindings.py."""

import json
from pathlib import Path

import pytest

import gen_bindings

FIXTURES = Path(__file__).resolve().parent / "fixtures"


def _run(tmp_path: Path, *extra: str, profile: str = "lab") -> tuple[int, Path]:
    out = tmp_path / f"ha-bindings.{profile}.draft.json"
    rc = gen_bindings.main([
        "--ttl", str(FIXTURES / "sample.ttl"),
        "--profile", profile,
        "--out", str(out),
        *extra,
    ])
    return rc, out


def test_extracts_sensors_and_supported_actuators_from_sample(tmp_path):
    rc, out = _run(tmp_path)
    assert rc == 0
    data = json.loads(out.read_text(encoding="utf-8"))
    assert data["profile"] == "lab"
    assert data["platform"] == "ha:HomeAssistantTest"

    sensor_ids = {s["haEntityId"] for s in data["sensors"]}
    assert "sensor.living_room_temperature" in sensor_ids
    assert "sensor.lab_humidity" in sensor_ids

    actuators = {a["haEntityId"]: a for a in data["actuators"]}
    assert actuators["light.kitchen"]["haKind"] == "Light"
    assert actuators["switch.lab_switch"]["haKind"] == "Switch"
    # Unsupported domains must NOT appear in actuators[] by default —
    # otherwise the draft would fail HaBindingsLoader validation.
    assert "climate.living_room" not in actuators


def test_unsupported_domain_lands_in_ignored_candidates_by_default(tmp_path):
    rc, out = _run(tmp_path)
    assert rc == 0
    data = json.loads(out.read_text(encoding="utf-8"))
    ignored = data.get("ignoredCandidates", [])
    climate = next(
        c for c in ignored if c["haEntityId"] == "climate.living_room"
    )
    assert climate["type"] == "actuator"
    assert "climate" in climate["reason"]


def test_default_actuators_all_pass_shape_validation(tmp_path):
    """The default draft must be loadable: no haKind=null in actuators[]."""
    import binding as _binding  # local import to keep module tidy
    rc, out = _run(tmp_path)
    assert rc == 0
    data = json.loads(out.read_text(encoding="utf-8"))
    for a in data["actuators"]:
        assert _binding.validate_binding_shape(a) == [], a
    for s in data["sensors"]:
        assert _binding.validate_binding_shape(s) == [], s


def test_include_unknown_keeps_climate_in_actuators_with_null_kind(tmp_path):
    rc, out = _run(tmp_path, "--include-unknown")
    assert rc == 0
    data = json.loads(out.read_text(encoding="utf-8"))
    climate = next(
        a for a in data["actuators"] if a["haEntityId"] == "climate.living_room"
    )
    assert climate["haKind"] is None
    # Still also recorded under ignoredCandidates for traceability.
    assert any(
        c["haEntityId"] == "climate.living_room"
        for c in data.get("ignoredCandidates", [])
    )


def test_explicit_procedure_uri_is_preserved(tmp_path):
    rc, out = _run(tmp_path)
    assert rc == 0
    data = json.loads(out.read_text(encoding="utf-8"))
    humidity = next(
        s for s in data["sensors"] if s["haEntityId"] == "sensor.lab_humidity"
    )
    assert humidity["procedureUri"].endswith("HumidityProcedure")


def test_refuses_to_overwrite_by_default(tmp_path):
    out = tmp_path / "ha-bindings.lab.draft.json"
    out.write_text("placeholder", encoding="utf-8")
    rc = gen_bindings.main([
        "--ttl", str(FIXTURES / "sample.ttl"),
        "--profile", "lab",
        "--out", str(out),
    ])
    assert rc == 1
    assert out.read_text(encoding="utf-8") == "placeholder"


def test_overwrite_flag_replaces_file(tmp_path):
    out = tmp_path / "ha-bindings.lab.draft.json"
    out.write_text("placeholder", encoding="utf-8")
    rc = gen_bindings.main([
        "--ttl", str(FIXTURES / "sample.ttl"),
        "--profile", "lab",
        "--out", str(out),
        "--overwrite",
    ])
    assert rc == 0
    assert json.loads(out.read_text(encoding="utf-8"))["profile"] == "lab"


def test_refuses_non_draft_filename(tmp_path):
    out = tmp_path / "ha-bindings.lab.json"
    rc = gen_bindings.main([
        "--ttl", str(FIXTURES / "sample.ttl"),
        "--profile", "lab",
        "--out", str(out),
    ])
    assert rc == 2
    assert not out.exists()


def test_refuses_non_json_extension(tmp_path):
    out = tmp_path / "ha-bindings.lab.draft.yaml"
    rc = gen_bindings.main([
        "--ttl", str(FIXTURES / "sample.ttl"),
        "--profile", "lab",
        "--out", str(out),
    ])
    assert rc == 2


def test_output_is_byte_for_byte_stable(tmp_path):
    out1 = tmp_path / "a.draft.json"
    out2 = tmp_path / "b.draft.json"
    for out in (out1, out2):
        rc = gen_bindings.main([
            "--ttl", str(FIXTURES / "sample.ttl"),
            "--profile", "lab",
            "--out", str(out),
        ])
        assert rc == 0
    assert out1.read_bytes() == out2.read_bytes()


def test_default_output_path_is_under_output_dir(tmp_path):
    out_dir = tmp_path / "config"
    rc = gen_bindings.main([
        "--ttl", str(FIXTURES / "sample.ttl"),
        "--profile", "lab",
        "--output-dir", str(out_dir),
    ])
    assert rc == 0
    expected = out_dir / "ha-bindings.lab.draft.json"
    assert expected.is_file()


def test_secret_argument_is_refused(tmp_path):
    out = tmp_path / "ha-bindings.lab.draft.json"
    rc = gen_bindings.main([
        "--ttl", str(FIXTURES / "sample.ttl"),
        "--profile", "Bearer abc.def.ghi",
        "--out", str(out),
    ])
    assert rc == 2
    assert not out.exists()


def test_jwt_like_value_is_refused(tmp_path):
    out = tmp_path / "ha-bindings.lab.draft.json"
    rc = gen_bindings.main([
        "--ttl", str(FIXTURES / "sample.ttl"),
        "--profile", "eyJhbGciOiJIUzI1NiJ9.payload.sig",
        "--out", str(out),
    ])
    assert rc == 2


def test_missing_ttl_returns_error(tmp_path):
    rc, out = _run(tmp_path, profile="lab")
    # Sanity baseline first
    assert rc == 0
    out2 = tmp_path / "ha-bindings.lab2.draft.json"
    rc2 = gen_bindings.main([
        "--ttl", str(tmp_path / "does-not-exist.ttl"),
        "--profile", "lab2",
        "--out", str(out2),
    ])
    assert rc2 == 1


def test_generate_bindings_warns_and_records_unsupported_domain(tmp_path):
    result, warnings = gen_bindings.generate_bindings(
        ttl_path=FIXTURES / "sample.ttl",
        profile="lab",
    )
    assert any("climate" in w for w in warnings), warnings
    # Default: not in actuators[], present in ignoredCandidates[].
    assert all(
        not a["haEntityId"].startswith("climate.") for a in result["actuators"]
    )
    assert any(
        c["haEntityId"].startswith("climate.")
        for c in result.get("ignoredCandidates", [])
    )


def test_generate_bindings_include_unknown_round_trips_into_actuators(tmp_path):
    result, _ = gen_bindings.generate_bindings(
        ttl_path=FIXTURES / "sample.ttl",
        profile="lab",
        include_unknown=True,
    )
    climate = next(
        a for a in result["actuators"] if a["haEntityId"] == "climate.living_room"
    )
    assert climate["haKind"] is None
