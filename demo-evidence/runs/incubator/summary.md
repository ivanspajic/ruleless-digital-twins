# Run: incubator

| Field | Value |
|---|---|
| Environment | `incubator` |
| Config rounds (`MaximumMapekRounds`) | ∞ |
| Look-ahead cycles | 3 |
| Mode | proactive |
| Ruleless | true |
| Exit code | `exit=35` |
| Decisions captured | 0 (none) |
| First timestamp | `20:03:20` |
| Last timestamp | `20:03:20` |
| Stdout lines | 31 |
| Stderr lines | 14 |
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

_No decisions captured. Check `stderr.log` if the run errored, or `stdout.log` for the actual output format._

## Files in this directory

| File | Purpose |
|---|---|
| `appsettings-used.json` | exact config the run consumed |
| `stdout.log`  | raw stdout of `dotnet run` |
| `stderr.log` | raw stderr (often empty on success) |
| `exit-code.txt` | process exit code |
| `summary.md` | this file |
