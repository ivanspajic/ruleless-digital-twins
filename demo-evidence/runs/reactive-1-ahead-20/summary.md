# Run: reactive-1-ahead-20

| Field | Value |
|---|---|
| Environment | `roomM370` |
| Config rounds (`MaximumMapekRounds`) | 20 |
| Look-ahead cycles | 1 |
| Mode | reactive |
| Ruleless | true |
| Exit code | `exit=0` |
| Decisions captured | 20 (mapek) |
| First timestamp | `20:01:24` |
| Last timestamp | `20:02:23` |
| Stdout lines | 13436 |
| Stderr lines | 0 |
| Log compressed | false |

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

1. `Dehumidifier=0,FloorHeating=1,Heater=0`
2. `Heater=0,Dehumidifier=0,FloorHeating=0`
3. `Dehumidifier=1,Heater=2,FloorHeating=0`
4. `Heater=0,FloorHeating=0,Dehumidifier=0`
5. `Dehumidifier=1,FloorHeating=0,Heater=2`
6. `FloorHeating=0,Dehumidifier=0,Heater=0`
7. `Dehumidifier=1,Heater=2,FloorHeating=0`
8. `FloorHeating=0,Dehumidifier=0,Heater=0`
9. `Heater=2,FloorHeating=0,Dehumidifier=1`
10. `FloorHeating=0,Heater=0,Dehumidifier=0`
11. `FloorHeating=0,Dehumidifier=1,Heater=2`
12. `Heater=0,Dehumidifier=0,FloorHeating=0`
13. `Dehumidifier=1,FloorHeating=0,Heater=2`
14. `Heater=0,FloorHeating=0,Dehumidifier=0`
15. `Heater=2,FloorHeating=0,Dehumidifier=1`
16. `FloorHeating=0,Heater=0,Dehumidifier=0`
17. `Dehumidifier=1,FloorHeating=0,Heater=2`
18. `Heater=0,Dehumidifier=0,FloorHeating=0`
19. `Heater=2,FloorHeating=0,Dehumidifier=1`
20. `FloorHeating=0,Dehumidifier=0,Heater=0`

## Files in this directory

| File | Purpose |
|---|---|
| `appsettings-used.json` | exact config the run consumed |
| `stdout.log`  | raw stdout of `dotnet run` |
| `stderr.log` | raw stderr (often empty on success) |
| `exit-code.txt` | process exit code |
| `summary.md` | this file |
