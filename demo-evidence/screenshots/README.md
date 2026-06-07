# screenshots — Technical screenshots

Ordered list of captures used as technical evidence. Save them as **PNG**, with
the exact file name below, in this folder.

| # | File | What to capture | Why |
|---|---|---|---|
| 01 | `01-smartnode-running.png` | The SmartNode window with `Internal API listening on http://localhost:8080/` and the mode (`chatbox-only`). | Proves the service is running. |
| 02 | `02-mapek-tick-response.png` | The JSON response of a `POST /api/mapek/tick` (scored scenarios + `selectedPlan`). | Proves the Observe→Score→Plan loop. |
| 03 | `03-mapek-decisions-count.png` | `GET /api/mapek/decisions` with `count` ≥ 1 and a decision. | Proves the decision log (Knowledge). |
| 04 | `04-home-assistant-dashboard.png` | The Home Assistant dashboard (`climate.*` entities, price, zone). | Home Assistant context (if HA available). |
| 05 | `05-protected-execution-denied.png` | A `dryRun:false` response that is **blocked**: `dryRun:true` + warning "Real execution blocked". | Proves the fail-closed safety. |
| 06 | `06-autonomous-loop-decisions.png` | The SmartNode window with `MAPE-K autonomous tick #N ok` **and** a `/api/mapek/decisions` whose `count` rose without curl. | Proves the autonomous loop. |
| 07 | `07-dashboard-goal-form.png` | The dashboard with the **Manage a goal** form, before creating a goal. | Shows the goal-editing UI affordance (P1-B). |
| 08 | `08-goal-created-in-dashboard.png` | After **Save goal**: the inline `Saved goal "living-room-comfort".` message and the new row in **Active goals**. | Proves a goal created from the UI. |
| 09 | `09-dry-run-decision-after-goal.png` | After **Run dry tick**: the `heat-now` decision (`dry-run`, `not executed`) in **Recent decisions** and the selected plan (`Executed=false; nothing sent to Home Assistant`). | Proves the create → tick → decision loop, nothing sent to HA. |

## Capture tips

- **Mask every token**: no capture must show a real `TOKEN_HA`, or any sensitive
  URL/identifier. Blur or crop.
- Readable window: large enough font, indented JSON (`ConvertTo-Json -Depth 8`).
- For 06, take the screenshot **after** ~20 s of autonomous mode so `count` ≥ 3.
- Keep visual consistency (same terminal theme).

## Checklist

```
[ ] 01-smartnode-running.png
[ ] 02-mapek-tick-response.png
[ ] 03-mapek-decisions-count.png
[ ] 04-home-assistant-dashboard.png   (if HA available)
[ ] 05-protected-execution-denied.png
[ ] 06-autonomous-loop-decisions.png
[ ] 07-dashboard-goal-form.png
[ ] 08-goal-created-in-dashboard.png
[ ] 09-dry-run-decision-after-goal.png
```
