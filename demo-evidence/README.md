# demo-evidence — MAPE-K technical evidence pack

This folder gathers **everything that proves** how the SmartNode MAPE-K loop
works: reproduction commands, JSON outputs, screenshots, and a technical
scenario. It is a reproducible evidence pack: nothing is invented at writing
time, and each claim is backed by commands or captured outputs.

> The outputs are JSON files captured from the SmartNode API.

## What this pack proves

The full loop, observable end to end:

```
User goal
  → Observe (Home Assistant snapshot + replay price)
  → Analyze (coded findings)
  → Score 3 scenarios (do-nothing / heat-now / wait-cheaper)
  → Plan (argmax selection)
  → Home Assistant action (dry-run, or real behind safety gates)
  → Decision logged (queryable via /api/mapek/decisions)
  ↻ on an autonomous loop (dry-run only)
```

Plus the **safety guarantees**:

- real execution is **fail-closed**: 5 cumulative gates, allowlist authoritative;
- the autonomous loop is **always dry-run**, even if `MAPEK_ALLOW_EXECUTION=true`.

## Layout

| File / folder | Content |
|---|---|
| [`demo-commands.md`](demo-commands.md) | All the PowerShell commands to reproduce the demo. |
| [`demo-scenario.md`](demo-scenario.md) | Technical walkthrough of the MAPE-K verification scenario. |
| [`outputs/`](outputs/) | Captured JSON outputs (regenerable — see `outputs/README.md`). |
| [`screenshots/`](screenshots/) | Screenshots (list to capture in `screenshots/README.md`). |

## Regeneration

The files under [`outputs/`](outputs/) are **point-in-time captures**. They can be
**regenerated** at any time by replaying the commands in
[`demo-commands.md`](demo-commands.md) — they are not sources of truth, just frozen
evidence.

## ⚠️ Security — never commit a secret

- **Never** a real `TOKEN_HA`, a real `.env`, a sensitive internal URL, or a Home
  Assistant database/log in this folder.
- In examples, use the placeholders `<TOKEN_HA>` and `<HA_URL>`.
- Before capturing a JSON, check that no token or personal data is in it; **redact**
  if needed.
- Screenshots must show **no** token (mask the area).
