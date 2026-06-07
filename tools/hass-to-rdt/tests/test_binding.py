"""Unit tests for tools/hass-to-rdt/binding.py."""

import pytest

import binding


class TestNormalizeEntityId:
    def test_basic_passes_through(self):
        assert binding.normalize_entity_id("light.kitchen") == "light.kitchen"

    def test_strips_and_lowercases(self):
        assert binding.normalize_entity_id("  Light.Kitchen  ") == "light.kitchen"

    def test_rejects_missing_dot(self):
        with pytest.raises(ValueError):
            binding.normalize_entity_id("invalid")

    def test_rejects_special_chars(self):
        with pytest.raises(ValueError):
            binding.normalize_entity_id("light.kitchen-lamp!")

    def test_rejects_none(self):
        with pytest.raises(ValueError):
            binding.normalize_entity_id(None)  # type: ignore[arg-type]


class TestInferHaKind:
    def test_light(self):
        assert binding.infer_ha_kind("light.kitchen") == "Light"

    def test_switch(self):
        assert binding.infer_ha_kind("switch.heater") == "Switch"

    def test_input_boolean(self):
        assert binding.infer_ha_kind("input_boolean.ev_charging") == "InputBoolean"

    def test_input_number(self):
        assert binding.infer_ha_kind("input_number.target_temperature") == "InputNumber"

    def test_input_select(self):
        assert binding.infer_ha_kind("input_select.mode") == "InputSelect"

    def test_sensor_domain_returns_none(self):
        assert binding.infer_ha_kind("sensor.temperature") is None

    def test_climate_returns_none(self):
        assert binding.infer_ha_kind("climate.living_room") is None

    def test_cover_returns_none(self):
        assert binding.infer_ha_kind("cover.garage") is None

    def test_unknown_domain_returns_none(self):
        assert binding.infer_ha_kind("lock.front_door") is None


class TestMakeSensorBinding:
    def test_default_procedure_is_synthesized(self):
        b = binding.make_sensor_binding(
            sensor_uri="http://x/Foo", entity_id="sensor.t",
        )
        assert b == {
            "sensorUri": "http://x/Foo",
            "procedureUri": "http://x/FooProcedure",
            "kind": "HomeAssistant",
            "haEntityId": "sensor.t",
        }

    def test_explicit_procedure_is_kept(self):
        b = binding.make_sensor_binding(
            sensor_uri="http://x/Foo",
            entity_id="sensor.t",
            procedure_uri="http://x/CustomProc",
        )
        assert b["procedureUri"] == "http://x/CustomProc"

    def test_empty_uri_is_rejected(self):
        with pytest.raises(ValueError):
            binding.make_sensor_binding(sensor_uri="", entity_id="sensor.t")

    def test_invalid_entity_id_is_rejected(self):
        with pytest.raises(ValueError):
            binding.make_sensor_binding(
                sensor_uri="http://x/Foo", entity_id="not_an_entity",
            )


class TestMakeActuatorBinding:
    def test_with_kind(self):
        b = binding.make_actuator_binding(
            actuator_uri="http://x/Light1",
            entity_id="light.kitchen",
            ha_kind="Light",
        )
        assert b == {
            "actuatorUri": "http://x/Light1",
            "kind": "HomeAssistant",
            "haEntityId": "light.kitchen",
            "haKind": "Light",
        }

    def test_without_kind_yields_null(self):
        b = binding.make_actuator_binding(
            actuator_uri="http://x/Climate1", entity_id="climate.lr",
        )
        assert b["haKind"] is None

    def test_invalid_kind_is_rejected(self):
        with pytest.raises(ValueError):
            binding.make_actuator_binding(
                actuator_uri="http://x/A",
                entity_id="light.k",
                ha_kind="NotARealKind",
            )

    def test_empty_uri_is_rejected(self):
        with pytest.raises(ValueError):
            binding.make_actuator_binding(
                actuator_uri="", entity_id="light.k", ha_kind="Light",
            )


class TestValidateBindingShape:
    def test_valid_sensor_has_no_errors(self):
        b = binding.make_sensor_binding(
            sensor_uri="http://x/F", entity_id="sensor.t",
        )
        assert binding.validate_binding_shape(b) == []

    def test_valid_actuator_has_no_errors(self):
        b = binding.make_actuator_binding(
            actuator_uri="http://x/A", entity_id="light.k", ha_kind="Light",
        )
        assert binding.validate_binding_shape(b) == []

    def test_actuator_with_null_haKind_flagged(self):
        b = {
            "actuatorUri": "http://x/A",
            "kind": "HomeAssistant",
            "haEntityId": "climate.lr",
            "haKind": None,
        }
        errs = binding.validate_binding_shape(b)
        assert any("haKind required" in e for e in errs)

    def test_unknown_kind_flagged(self):
        b = {
            "actuatorUri": "http://x/A",
            "kind": "HomeAssistant",
            "haEntityId": "light.k",
            "haKind": "NotARealKind",
        }
        errs = binding.validate_binding_shape(b)
        assert errs

    def test_constant_sensor_without_value_flagged(self):
        b = {
            "sensorUri": "http://x/F",
            "procedureUri": "http://x/FProc",
            "kind": "Constant",
        }
        errs = binding.validate_binding_shape(b)
        assert any("constantValue required" in e for e in errs)

    def test_neither_uri_flagged(self):
        errs = binding.validate_binding_shape({"foo": "bar"})
        assert errs

    def test_non_dict_flagged(self):
        errs = binding.validate_binding_shape("not a dict")  # type: ignore[arg-type]
        assert errs
