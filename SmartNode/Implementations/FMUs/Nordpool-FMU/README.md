# Nord Pool FMU

This project generates a Functional Mockup Unit (FMU) folllowing FMI 2 that uses Nord Pool's HTTP API to request and provide day-ahead electricity spot prices.
The FMU requires https://github.com/NTNU-IHB/PythonFMU to build, and a matching Python installation at runtime.

The code contains a fork of the Python [`nordpool`-module](https://github.com/kipe/nordpool) which has been adapted to use `requests_cache` for efficiency and to avoid Nord Pool's rate-limiting, see [LICENSE-nordpool.txt](LICENSE-nordpool.txt).

**NB:** you should really install `PythonFMU > v0.6.9` from source (not `pip`), i.e. most likely from `master`-branch!

## Design decisions

* `time` is the only input (in seconds)
* `zone` (string) and `resolution` (int) are parameters
* `price` (real, output)
* `notFound` (bool, output) -- true if there was no data for the simulated time

The FMU stores the current system time when `fmi2SetupExperiment` is called, and adds `start_time` in seconds to that.
Subsequently, simulated time advances. See `Examples` below.

## Building & Installation

This project will result in an FMU containing the binary components included from `PythonFMU`.
As the `PythonFMU`-package that your package-manager may offer you, may not containt your platform-specific binaries, I recommend that you also install `PythonFMU` from source. As of this writing, at least Linux `arm64` and MacOS `darwin64` binary components are not provided.

## (Unit-) Testing & Examples

To check basic functionality, you can use invoke `python NordPool-fmu.py` (after installing the dependencies):

```
Nordpool-FMU$ pip install -r requirements.txt
Nordpool-FMU$ python NordPool-fmu.py
2025-11-19 13:11:58.238633+00:00
117.48
```

The generated FMU can be tested e.g. via [`FMPy`](https://github.com/CATIA-Systems/FMPy) or [`cosim`](https://github.com/open-simulation-platform/cosim-cli$0). You'll have to install `pythonfmu` separately using `pip`/`pipx`, as the included `requirements.txt` is strictly only for the code **in** the FMU.

```
Nordpool-FMU$ git clone <PythonFMU> ...
Nordpool-FMU$ pythonfmu build -f NordPool-fmu.py requirements.txt nordpool
Nordpool-FMU$ ls -l NordPool.fmu 
-rw-rw-r-- 1 vs vs 659530 Nov 19 10:53 NordPool.fmu
Nordpool-FMU$ fmpy info NordPool.fmu 

Model Info

  FMI Version        2.0
  FMI Type           Co-Simulation
  Model Name         NordPool
  Description        NordPool FMU
  Platforms          linux64, win64
  Continuous States  0
  Event Indicators   0
  Variables          4
  Generation Tool    PythonFMU 0.7.0
  Generation Date    2025-11-19T09:53:33+00:00


Variables (input, output)

  Name               Causality              Start Value  Unit     Description
  price              output                                     
  notFound           output                                     
```

Running a simulation starting some time in the future (20.000s ~ 5,5h), for another 5,5h, with 60 minute intervals, asking Nord Pool for data on zone NO5 (case-sensitive!) with 60 minute resolution (they also support 15 minute resolution):

```
Nordpool-FMU$ fmpy simulate --start-time 1800 --stop-time 40000 --output-interval 3600 --output-file no5.csv --start-values resolution 60 zone NO5 -- NordPool.fmu
NordPool-FMU: didn't get any results for time 2025-11-19 23:04:48.654245+00:00, zone NO5, resolution 60.
```

The warning occurs because the API didn't return any data that far ahead in the future. Looking at the results shows the point in time where we ran out of available data:

```
Nordpool-FMU$ cat no5.csv 
"time","price","notFound"
1800.0,112.0,False
5400.0,117.48,False
9000.0,152.43,False
12600.0,159.69,False
16200.0,158.03,False
19800.0,115.84,False
23400.0,115.51,False
27000.0,102.82,False
30600.0,97.32,False
34200.0,96.25,False
37800.0,89.22,False
40000.0,0.0,True
```

## To-Do

* Handle difference in method signature(s) between release `PythonFMU` and `main`-branch, e.g. on `setup_experiment`.
* Look into `PythonFMU`'s `deploy` mechanism to install dependencies when actually using the FMU.
* Look into occassional non-deterministic core dumps which are most likely not our fault.

## License

This FMU is licensed under [CC BY-SA 4.0](LICENSE.txt).
