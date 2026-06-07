# outputs — Captured JSON outputs

Frozen captures of the SmartNode API responses. **Regenerable** by replaying the
commands in [`../demo-commands.md`](../demo-commands.md). These are not sources of
truth, just dated evidence.

## Files to produce

| File | Source | Command (ref.) |
|---|---|---|
| `tick-dryrun-response.json` | `POST /api/mapek/tick {"dryRun": true}` | demo-commands § d |
| `decisions-after-autonomous-loop.json` | `GET /api/mapek/decisions` after the autonomous loop | demo-commands § g |
| `price-replay-response.json` | `GET /api/price` (rebased replay) | demo-commands § c |
| `protected-execution-denied-response.json` | `POST /api/mapek/tick {"dryRun": false}` without the master switch | demo-commands § h |

## What each file proves

- **tick-dryrun-response.json** — the full loop in one call: goals, 3 scored
  scenarios, `heat-now` plan, action `executed:false`, analysis, decision.
- **decisions-after-autonomous-loop.json** — the system ticks **on its own**:
  `count` > 1, decisions spaced by the interval, all `dryRun:true`.
- **price-replay-response.json** — the current price is **non-null** thanks to the
  replay-sample rebase.
- **protected-execution-denied-response.json** — real execution is **blocked** when
  a gate is missing (forced `dryRun:true` + warning).

## ⚠️ Before committing a JSON

- Check that it contains **no** `TOKEN_HA`, token, or personal data.
- In offline mode (`TOKEN_HA=''`), the responses contain no secret, but **re-read**
  them anyway before committing.
- Redact any sensitive value with `<REDACTED>` if needed.

> Tip: use `Out-File -Encoding utf8` to write the files (see demo-commands).
