"""Tests for the ConfigurableParameter (continuous lever) support in
hacvt_rdt.py: the discrete-level helper and the field-priority constant."""
import pytest

from hacvt_rdt import HACVT_RDT, _LEVER_FIELD_PRIORITY, _CONFIG_PARAM_STEPS


class TestDiscreteLevels:
    def test_three_steps_0_300(self):
        assert HACVT_RDT._discrete_levels(0, 300, 3) == [0.0, 150.0, 300.0]

    def test_three_steps_0_100(self):
        assert HACVT_RDT._discrete_levels(0, 100, 3) == [0.0, 50.0, 100.0]

    def test_five_steps(self):
        assert HACVT_RDT._discrete_levels(0, 100, 5) == [0.0, 25.0, 50.0, 75.0, 100.0]

    def test_two_steps_are_extremes(self):
        assert HACVT_RDT._discrete_levels(0, 300, 2) == [0.0, 300.0]

    def test_degenerate_min_equals_max(self):
        # No span -> single value, never crashes.
        assert HACVT_RDT._discrete_levels(5, 5, 3) == [5.0]

    def test_one_step(self):
        assert HACVT_RDT._discrete_levels(0, 300, 1) == [0.0]

    def test_non_numeric_bounds_do_not_crash(self):
        assert HACVT_RDT._discrete_levels("x", None, 3) == []

    def test_default_step_count_is_three(self):
        assert _CONFIG_PARAM_STEPS == 3


class TestLeverFieldPriority:
    def test_brightness_preferred_over_transition(self):
        # brightness must be ranked (present) and transition must not be,
        # so the "one lever per actuator" picker chooses brightness.
        assert "brightness" in _LEVER_FIELD_PRIORITY
        assert "brightness_pct" in _LEVER_FIELD_PRIORITY
        assert "transition" not in _LEVER_FIELD_PRIORITY

    def test_priority_is_ordered(self):
        # brightness ranks before color_temp (more meaningful as a lever).
        assert _LEVER_FIELD_PRIORITY.index("brightness") < _LEVER_FIELD_PRIORITY.index("color_temp")
