"""Shared pytest fixtures for tools/hass-to-rdt tests."""

import socket
import sys
from pathlib import Path

import pytest

# Make ``binding`` and ``gen_bindings`` importable without an editable install.
_TOOL_ROOT = Path(__file__).resolve().parent.parent
if str(_TOOL_ROOT) not in sys.path:
    sys.path.insert(0, str(_TOOL_ROOT))


@pytest.fixture(autouse=True)
def block_network(monkeypatch):
    """Refuse any TCP socket.connect call inside the test process."""
    def _refuse(*_args, **_kwargs):
        raise RuntimeError("network access is not allowed in tests")
    monkeypatch.setattr(socket.socket, "connect", _refuse)
