# Smart Home Assistant UI Notes

## Structure

The demo UI is still a vanilla HTML/CSS/JS page in `SmartNode/SmartNode/index.html`.
It is organized as:

- Sidebar navigation with demo commands and a suggested presentation flow.
- Main workspace with live SmartNode, Home Assistant, and energy-source status.
- Integrated guide for supported categories: lights, switches, climate, sensors, energy price, scenes, and scripts.
- Chat panel with grouped quick actions, typed/loading state, message timestamps, and response source labels.

## Energy Price Routing

Generic energy price requests such as `what is the energy price`, `energy price`, `nord pool price`, `prix de l'energie`, and `prix electricite` must continue to route to `/api/price`.

Specific sensor requests such as `state of Nord Pool NO5 Current price` must continue through the Home Assistant entity resolver so the exact sensor state can be queried.

## Manual Validation Commands

Use the local SmartNode UI on `localhost:8080` and test:

- `what can you control?`
- `what's the house status`
- `what is the energy price`
- `energy price`
- `nord pool price`
- `state of Nord Pool NO5 Current price`
- `turn off all lights`
- `turn on Testlab Eco Day`

Repository checks:

```powershell
git diff --check
dotnet build .\SmartNode\SmartNode.sln --nologo
```

## Files Touched

- `SmartNode/SmartNode/index.html`
- `docs/UI_CHAT_DESIGN.md`

## Known Limits

- The dashboard does not invent fallback values; empty cards stay empty until the relevant Home Assistant or `/api/price` endpoint returns data.
- Quick actions are generated from the live Home Assistant entity catalog, so available light, scene, and script shortcuts vary by instance.
- The UI remains dependency-free and does not load external fonts or CDNs.
