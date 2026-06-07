# Run: bang-bang

| Field | Value |
|---|---|
| Environment | `roomM370` |
| Config rounds (`MaximumMapekRounds`) | 20 |
| Look-ahead cycles | 1 |
| Mode | proactive |
| Ruleless | false |
| Exit code | `exit=0` |
| Decisions captured | 20 (bang-bang) |
| First timestamp | `20:00:31` |
| Last timestamp | `20:00:38` |
| Stdout lines | 4823 |
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

1. `Heater=1,FloorHeating=1,Dehumidifier=0`
2. `Heater=0,FloorHeating=0,Dehumidifier=0`
3. `Heater=1,FloorHeating=1,Dehumidifier=1`
4. `Heater=0,FloorHeating=0,Dehumidifier=0`
5. `Heater=1,FloorHeating=1,Dehumidifier=1`
6. `Heater=0,FloorHeating=0,Dehumidifier=0`
7. `Heater=1,FloorHeating=1,Dehumidifier=1`
8. `Heater=0,FloorHeating=0,Dehumidifier=0`
9. `Heater=1,FloorHeating=1,Dehumidifier=1`
10. `Heater=0,FloorHeating=0,Dehumidifier=0`
11. `Heater=1,FloorHeating=1,Dehumidifier=1`
12. `Heater=0,FloorHeating=0,Dehumidifier=0`
13. `Heater=1,FloorHeating=1,Dehumidifier=1`
14. `Heater=0,FloorHeating=0,Dehumidifier=0`
15. `Heater=1,FloorHeating=1,Dehumidifier=1`
16. `Heater=0,FloorHeating=0,Dehumidifier=0`
17. `Heater=1,FloorHeating=1,Dehumidifier=1`
18. `Heater=0,FloorHeating=0,Dehumidifier=0`
19. `Heater=1,FloorHeating=1,Dehumidifier=1`
20. `Heater=0,FloorHeating=0,Dehumidifier=0`

## Files in this directory

| File | Purpose |
|---|---|
| `appsettings-used.json` | exact config the run consumed |
| `stdout.log`  | raw stdout of `dotnet run` |
| `stderr.log` | raw stderr (often empty on success) |
| `exit-code.txt` | process exit code |
| `summary.md` | this file |
