## Introduction
This is the accompanying artifact to the Spajić and Stolz 2025 DataMod paper ([preprint as PDF](http://foldr.org/selabhvl/2025/2025-datamod-preprint.pdf)). It consists of an ontology, instance models, an inference engine JAR and source code, inference (and verification) rules, control loop (logic) codebase, and a simulation model (FMU) and its source code.

## Overview of the System
Coming soon!

## Requirements
- .NET 8 (for running the control loop coordinator)
- Java (for running the inference engine)
- MongoDB (for hosting the case repository - not needed if you are not using this)

## Local Development (Windows)

This fork is set up so a fresh clone builds and tests cleanly on Windows without
external git submodule fetches. All previous submodules (Femyou, Nordpool-FMU,
PythonFMU) are vendored directly into the repo.

### Prerequisites

- **.NET 8 SDK** — for building/testing the solution.
- **Java 21** (any modern JDK works) — for the inference engine JAR.
- **Docker Desktop** — only if you want the live HA / MongoDB / RabbitMQ tests.

Optional, only if you want to run the PythonFMU-backed tests (NordPool, Fakepool):
- **Python 3.11 from python.org** (the regular installer, NOT the Microsoft Store
  build and NOT the embeddable ZIP — both have DLL loading constraints that
  break the FMU runtime).

### Build

```powershell
dotnet build SmartNode/SmartNode.sln
```

### Default test run

```powershell
dotnet test SmartNode/SmartNode.sln
```

Expected: **16 passed / 16 skipped / 0 failed**. Skipped tests need extra setup
(see below).

### Bringing up the supporting services

Most non-trivial tests and the `dotnet run` path expect at least one of:

- **Home Assistant** on `http://localhost:8123` — for HA sensor tests.
- **MongoDB** on `localhost:27017` — for the case-based reasoning path.
- **RabbitMQ** on `localhost:5672` — for the incubator AMQP path.

A vendored compose file in `services/` brings up Mongo + RabbitMQ:

```powershell
docker compose -f services/docker-compose.demo.yml up -d mongodb rabbitmq
```

HA is left out of the bring-up command because the typical setup already has an
`ha-instance` container running. If you don't have one, either uncomment the
`homeassistant` service in `services/docker-compose.demo.yml` and run the
command without `mongodb rabbitmq`, or use any other HA image you trust.

### Home Assistant tests

`TestProject/HomeAssistantSensorTest.cs` includes a `Local` case that targets
`http://localhost:8123/sensor.showcase_living_room_temperature`. To enable it,
store a long-lived HA token in `dotnet user-secrets`:

```powershell
cd SmartNode/TestProject
dotnet user-secrets set "HA:TOKEN_LOCAL" "<your_long_lived_HA_token>"
```

The token is created from the HA UI under *Profile → Long-Lived Access Tokens*.
Run `dotnet test --filter "FullyQualifiedName~HomeAssistantSensorTest"` to
exercise the path. The pre-existing `MH30` / `IoTLab*` cases target external HA
instances and will stay skipped unless you also set `HA:TOKEN` and
`HA:TOKEN_IOTLAB` (only useful if you have access to those instances).

### PythonFMU-backed tests (NordPool, Fakepool)

These need a Python 3.11 runtime reachable from the test process plus the
`pythonfmu` package and the FMU's own Python dependencies.

1. **Install Python 3.11.x from python.org** (not Microsoft Store, not
   embeddable). Use the regular installer and accept the default user-mode
   install.
2. **Install the Python packages** the FMUs need:
   ```powershell
   py -3.11 -m pip install pythonfmu==0.6.9 pytz requests_cache python-dateutil
   ```
3. **Point the test runtime at that Python install** before running the tests.
   `FmuTestRuntime` reads two env vars:
   - `PYTHONFMU_RUNTIME_DIR` — directory containing `python3.dll`
     (the Python 3.11 install root).
   - `PYTHONFMU_PYTHON_EXE` — full path to `python.exe` from the same install.
     Used to auto-discover `pythonfmu-export.dll`. Falls back to
     `PYTHONFMU_EXPORT_DIR` if auto-discovery fails.

   Example (adjust paths to your install):
   ```powershell
   $py = (py -3.11 -c "import sys, os; print(os.path.dirname(sys.executable))")
   $env:PYTHONFMU_RUNTIME_DIR = $py
   $env:PYTHONFMU_PYTHON_EXE  = "$py\python.exe"
   dotnet test SmartNode/SmartNode.sln
   ```

If `PYTHONFMU_RUNTIME_DIR` is not set, the NordPool and Fakepool tests skip
cleanly with a self-explanatory message.

### Rebuilding the Fakepool FMU for the current platform

`SmartNode/Implementations/FMUs/Fakepool.fmu` is rebuilt from sources in
`SmartNode/Implementations/FMUs/Fakepool-FMU/`. To regenerate it with a native
binary for the host platform:

```powershell
cd SmartNode/Implementations/FMUs/Fakepool-FMU
pythonfmu build --no-external-tool -f Fakepool.py fakepool.tsv requirements.txt
Copy-Item Fakepool.fmu ..\Fakepool.fmu -Force
```

The `pythonfmu` CLI ships with the `pythonfmu` PyPI package installed above. The
resulting Fakepool.fmu contains `binaries/<platform>/Fakepool.dll` (Windows) or
`Fakepool.so` (Linux).

### Running the SmartNode app

```powershell
cd SmartNode/SmartNode
dotnet run --no-launch-profile -- --basedir "<absolute_path_to_repo_root>"
```

The default `Properties/appsettings.json` runs the M370 room scenario. To run
the incubator scenario, pass `--appsettings appsettings-incubator.json`. Other
preconfigured profiles in `Properties/` exercise different look-ahead horizons
and fuzziness levels.

## Docker-based example

The Dockerfile builds and runs the example inside the container. Note that `arm64` (and hence e.g. Apple Silicon) is currently not supported by one of the libraries that we depend on; see below for a workaround.
We use OpenModelica inside the container to compile the example FMU(s) into matching binaries.

```
% docker build -t smartnode -f SmartNode/Dockerfile SmartNode
...
=> => unpacking to docker.io/library/smartnode:latest
% docker run -v `pwd`/models-and-rules:/app/models-and-rules -v `pwd`/ontology:/app/ontology --rm -it smartnode
info: Logic.Mapek.MapekManager[0]
      Starting the MAPE-K loop.
...
```
### Docker-based incubator

To control the AU Incubator with the RDT:
1. Start Incubator with only the following daemons:
```
startup/startup_all_services.py:
    ...
    start_as_daemon(start_incubator_realtime_mockup)
    start_as_daemon(start_low_level_driver_mockup)
    start_as_daemon(start_influx_data_recorder)
```
2. Create a Docker network and connect the running RabbitMQ-server:
```
$ docker network create incubator
$ docker network connect rabbitmq-server incubator
```
3. Launch the RDT with the corresponding configuration. Use `docker network inspect incubator` to find out which IP address your RabbitMQ-server is using.
```
$ docker run --network incubator -e AU_INCUBATOR_RABBITMQ_HOST_NAME=172.20.0.2 -v `pwd`/models-and-rules:/app/models-and-rules -v `pwd`/ontology:/app/ontology --rm -it smartnode --appsettings appsettings-incubator.json
```

You should then see the RDT taking control of the incubator, switching on the fan and the heat.
Note that while the temperature is **within** the optimal range, as the current model doesn't have any other constraint,
the RDT may non-deterministically decide to run the heater or not. Only when it reaches the lower or upper boundary,
again a unique decision will be made.

### MongoDB in Docker
Since MongoDB is required to use the case-based functionality, there are some setup steps required to make it run (and persist) in Docker:
1. Create a network in Docker:
```
docker network create <your_network_name>
```
2. Connect the MongoDB and coordinator containers to the newly-created network:
```
docker network connect <your_network_name> <container_name>
```

## Running the Control Loop Coordinator (SmartNode)
The codebase is a .NET 8 solution consisting of multiple projects: `Logic` (MAPE-K and models), `Implementations` (for user-provided sensor/actuator implementations), `SmartNode` (startup and configuration project), and `TestProject` (unit and integration tests). The codebase also vendors the [Femyou](https://codeberg.org/SELab_HVL/vsto-Femyou) FMU loader (in `SmartNode/Femyou/`) with a Windows-specific patch that prepends the FMU's `binaries/<platform>/` directory to `PATH` before `LoadLibrary`, so dependencies such as `libwinpthread-1.dll` resolve correctly. Users may choose between running the solution natively or containerized.

The codebase uses a `appsettings.json` in the `SmartNode/Properties` directory as a configuration file. This file already comes preconfigured, but users are free to change their own settings. It contains the following parameters:
1. Filepath arguments:
  - `InferenceEngineFilepath`: the filepath of the inference engine JAR file.
  - `OntologyFilepath`: the filepath of the `ruleless-digital-twins.ttl` ontology.
  - `InstanceModelFilepath`: the filepath of the ontological instance model that describes the TT components and all properties and conditions of interest.
  - `InferenceRulesFilepath`: the filepath of the inference rules used for inferring information from the instance model.
  - `InferredModelFilepath`: the filepath of the inferred model to be created upon inference.
  - `FmuDirectory`: the solution's FMU storage directory.
  - `DataDirectory`: the solution's data storage directory for persisting data values from MAPE-K cycles.
2. Coordinator settings:
  - `SimulatedEnvironment`: a string for selecting a preconfigured simulated environment implementation. Leave blank if you wish to use a real one.
  - `SaveMapekData`: a boolean for saving MAPE-K cycle data to the disk.
  - `StartInReactiveMode`: a boolean for setting the starting mode of the coordinator. Running it in reactive mode means the system will only simulate corrective actions given the respective system actuators to mitigate the current optimal condition violations. In case of no violations of optimal conditions, the system will not simulate actions. In proactive mode, the system takes a proactive approach and simulates regardless of optimal condition status, subsequently including all existing system actuators. As a result, the proactive approach checks for potential violations of optimal conditions before they happen. Conversely, the reactive approach requires less simulating and is thus more performant.
  - `UseCaseBasedFunctionality`: a boolean for using the functionality where the system uses previously-saved actions for already encountered conditions.
  - `MaximumMapekRounds`: the maximum number of MAPE-K cycles to run before termination. Setting this value to -1 runs the solution indefinitely.
  - `SimulationDurationSeconds`: sets the duration of simulations in FMU time (not real-world time).
  - `LookAheadMapekCycles`: the number of cycles to simulate the future for. Simulating further ahead can yield more optimal decisions in the long run, but more cycles generally means less prediction accuracy and more performance overhead.
  - `PropertyValueFuzziness`: to match encountered conditions with potentially preexisting solutions, a quantization technique is applied to enable matching against (virtually) infinite numbers of property values.
3. Database settings:
  - `ConnectionString`: the MongoDB server connection string.
  - `DatabaseName`: the name of the case database.
  - `CollectionName`: the name of the case collection in MongoDB.

The Logic project provides interfaces in the `Logic.DeviceInterfaces` directory for users to implement when providing their own custom connections to sensors and actuators. It also provides the `IValueHandler` interface for user-provided implementations of logic handling various operations with specific OWL types. The solution contains the `DoubleValueHandler`, `IntValueHandler`, and `TimespanValueHandler` as example implementations in the `SensorActuatorImplementations` project. These are registered in the `Factory` in the `SmartNode` project, where the user is expected to register other custom implementations as well.

The solution currently runs based on the example 1 instance model, found in `instance-model-1.ttl` in the `models-and-rules` directory. Running this through the inference engine produces `inferred-model-1.ttl` which is used throughout the control loop. You may also use `inferred-model-2.ttl`, representing cyber components.

By default, the solution runs with a fake (dummy) environment as its twinning target, but users can easily add their own implementations of real devices via the `Factory` class. 

The instance model contains two `OptimalConditions` that are satisfied in the first cycle by the dummy values provided by dummy sensor implementations. Users are welcome to add their own or change the value to see the effects throughout the loop. There are many logging statements showing various stages of execution as well as the specific SPARQL queries and their results. At the end of each control loop cycle, the solution should print its chosen combination of actions to take, demonstrating what the DT's decision for that cycle would be.

## Running the Inference Engine Manually
The `ruleless-digital-twins-inference-engine.jar` file (available with models and rules) can also be executed manually from the console with the following 4 arguments provided:
1. The filepath of the ontology.
2. The filepath of the instance model (that uses the ontology).
3. The filepath of the inference rules. In case of multiple inference rules files, this filepath should be of the main file that includes the others.
4. The output filepath of the inferred model.

### Example
```
$ java -jar ruleless-digital-twins-inference-engine.jar ../Ontology/ruleless-digital-twins.ttl instance-model-1.ttl inference-rules.rules inf-out.ttl
```

If you are using multiple `.rules` files for inferencing, then you must make sure to match the `@include` directive filepaths in the main `.rules` file with the placement of the JAR file executing it. This repository contains a `verification-rules.rules` file which is included by the main `inference-rules.rules` file. This means that the filepath listed under the `@include` directive by default forces the JAR file to be executed from the same directory. Executing the JAR file from another directory will therefore require updating the filepath in the `@include` directive of the `inference-rules.rules` file.

Users are also encouraged to add their own files through the included `user-rules.rules` file.

### Requirements for Using Cyber TTs
In our solution, cyber TTs are treated similarly to physical TTs. This means they use instance models conformant to the same ontology and use user-provided implementations to connect to soft sensors and software for reconfiguration as well for providing possible values for ConfigurableParameters.