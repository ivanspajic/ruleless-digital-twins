## Introduction
This is the accompanying artifact to the Spajić and Stolz 2025 DataMod paper ([preprint as PDF](http://foldr.org/selabhvl/2025/2025-datamod-prepreprint.pdf)). It consists of an ontology, instance models, an inference engine JAR and source code, inference (and verification) rules, control loop (logic) codebase, and a simulation model (FMU) and its source code.

## Overview of the System
Coming soon!

## Requirements
- .NET 8 (for running the control loop coordinator)
- Java (for running the inference engine)
- MongoDB (for hosting the case repository)

## Docker-based example

The Dockerfile builds and runs the example inside the container. Note that `arm64` (and hence e.g. Apple Silicon) is currently not supported by one of the libraries that we depend on; see below for a workaround.
We use OpenModelica inside the container to compile the example FMU(s) into matching binaries.

```
% docker build -t smartnode -f SmartNode/SmartNode/Dockerfile SmartNode
...
=> => unpacking to docker.io/library/smartnode:latest
% docker run --rm -v `pwd`/models-and-rules:/app/models smartnode /app/models/inferred-model-1.ttl
info: Logic.Mapek.MapekManager[0]
      Starting the MAPE-K loop.
...
```

### Run the pre-built Dockerimage from Docker Hub via emulation:

```
% docker pull volkers/smartnode
Using default tag: latest
latest: Pulling from volkers/smartnode
...
% docker run --rm --platform linux/amd64 -v `pwd`/models-and-rules:/app/models volkers/smartnode /app/models/inferred-model-1.ttl
```

## Running the Control Loop Coordinator (SmartNode)
The codebase is a .NET 8 solution consisting of multiple projects: `Logic` (MAPE-K and models), `Implementations` (for user-provided sensor/actuator implementations), `SmartNode` (startup and configuration project), and `TestProject` (unit and integration tests). We also include our own fork of [Femyou](https://github.com/Oaz/Femyou) for the logic that loads and executes our FMUs. Users may choose between running the solution natively or containerized.

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
  - `MaximumMapekRounds`: the maximum number of MAPE-K cycles to run before termination. Setting this value to -1 runs the solution indefinitely.
  - `UseSimulatedEnvironment`: a boolean value for using the preconfigured simulated environment or a real one.
  - `SaveMapekData`: a boolean for saving MAPE-K cycle data to the disk.
  - `SimulationDurationSeconds`: sets the duration of simulations in FMU time (not real-world time).
  - `LookAheadMapekCycles`: the number of cycles to simulate the future for. Simulating further ahead can yield more optimal decisions in the long run, but more cycles generally means less prediction accuracy and more performance overhead.
  - `StartInReactiveMode`: a boolean for setting the starting mode of the coordinator. Running it in reactive mode means the system will only simulate corrective actions given the respective system actuators to mitigate the current optimal condition violations. In case of no violations of optimal conditions, the system will not simulate actions. In proactive mode, the system takes a proactive approach and simulates regardless of optimal condition status, subsequently including all existing system actuators. As a result, the proactive approach checks for potential violations of optimal conditions before they happen. Conversely, the reactive approach requires less simulating and is thus more performant.
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