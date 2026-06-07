# Run: default

| Field | Value |
|---|---|
| Environment | `roomM370` |
| Config rounds (`MaximumMapekRounds`) | 4 |
| Look-ahead cycles | 1 |
| Mode | proactive |
| Ruleless | true |
| Exit code | `exit=0` |
| Decisions captured | 4 (mapek) |
| First timestamp | `20:00:05` |
| Last timestamp | `20:00:20` |
| Stdout lines | 4048 |
| Stderr lines | 0 |
| Log compressed | false |

## Command

```powershell
cd SmartNode/SmartNode
dotnet run --no-launch-profile -- --basedir "<repo root>" 
```

For non-default profile, the exact appsettings file used is `appsettings-used.json` next to this summary. The actual command was equivalent to:

```powershell
dotnet run --no-launch-profile -- --basedir "<repo root>" 
```

## Decisions (per cycle)

1. `Heater=0,FloorHeating=1,Dehumidifier=0`
2. `Heater=0,FloorHeating=1,Dehumidifier=0`
3. `Heater=0,FloorHeating=1,Dehumidifier=1`
4. `Dehumidifier=0,FloorHeating=1,Heater=0`

## Files in this directory

| File | Purpose |
|---|---|
| `appsettings-used.json` | exact config the run consumed |
| `stdout.log`  | raw stdout of `dotnet run` |
| `stderr.log` | raw stderr (often empty on success) |
| `exit-code.txt` | process exit code |
| `summary.md` | this file |
