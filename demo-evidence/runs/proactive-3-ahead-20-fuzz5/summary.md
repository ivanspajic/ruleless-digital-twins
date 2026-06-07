# Run: proactive-3-ahead-20-fuzz5

| Field | Value |
|---|---|
| Environment | `roomM370` |
| Config rounds (`MaximumMapekRounds`) | 20 |
| Look-ahead cycles | 3 |
| Mode | proactive |
| Ruleless | true |
| Exit code | `exit=0` |
| Decisions captured | 7 (mapek) |
| First timestamp | `20:02:35` |
| Last timestamp | `20:03:07` |
| Stdout lines | 193668 |
| Stderr lines | 0 |
| Log compressed | true |

## Command

```powershell
cd SmartNode/SmartNode
dotnet run --no-launch-profile -- --basedir "<repo root>" --appsettings 
```

For non-default profile, the exact appsettings file used is `appsettings-used.json` next to this summary. The actual command was equivalent to:

```powershell
dotnet run --no-launch-profile -- --basedir "<repo root>" --appsettings <profile-name>.json
```

## Decisions (per cycle)

1. `FloorHeating=0,Heater=2,Dehumidifier=0,Dehumidifier=0,FloorHeating=0,Heater=1,FloorHeating=1,Heater=0,Dehumidifier=1`
2. `FloorHeating=0,Heater=2,Dehumidifier=0,FloorHeating=0,Dehumidifier=0,Heater=1,Heater=0,FloorHeating=1,Dehumidifier=1`
3. `FloorHeating=0,Heater=2,Dehumidifier=0,Dehumidifier=0,FloorHeating=0,Heater=1,FloorHeating=1,Heater=0,Dehumidifier=1`
4. `Heater=2,FloorHeating=0,Dehumidifier=0,Heater=1,FloorHeating=0,Dehumidifier=0,FloorHeating=1,Heater=0,Dehumidifier=1`
5. `FloorHeating=0,Heater=2,Dehumidifier=0,FloorHeating=0,Heater=1,Dehumidifier=0,FloorHeating=1,Heater=0,Dehumidifier=1`
6. `FloorHeating=0,Heater=2,Dehumidifier=0,Dehumidifier=0,FloorHeating=0,Heater=1,Heater=0,FloorHeating=1,Dehumidifier=1`
7. `Dehumidifier=0,FloorHeating=0,Heater=2,FloorHeating=0,Heater=1,Dehumidifier=0,Heater=0,FloorHeating=1,Dehumidifier=1`

## Files in this directory

| File | Purpose |
|---|---|
| `appsettings-used.json` | exact config the run consumed |
| `stdout.log` (`.gz`) | raw stdout of `dotnet run` |
| `stderr.log` | raw stderr (often empty on success) |
| `exit-code.txt` | process exit code |
| `summary.md` | this file |
