## Introduction
This is the accompanying artifact to the Spajić and Stolz 2025 DataMod paper ([preprint as PDF](http://foldr.org/selabhvl/2025/2025-datamod-prepreprint.pdf)). It consists of an ontology, instance models, an inference engine JAR and source code, inference (and verification) rules, control loop (logic) codebase, and a simulation model (FMU) and its source code.

## Running the Inference Engine
The `ruleless-digital-twins-inference-engine.jar` file (available with models and rules) should be executed from the console with 4 arguments provided:
1. The filepath of the ontology.
2. The filepath of the instance model (that uses the ontology).
3. The filepath of the inference rules. In case of multiple inference rules files, this filepath should be of the main file that includes the others.
4. The output filepath of the inferred model.

If you are using multiple `.rules` files for inferencing, then you must make sure to match the `@include` directive filepaths in the main `.rules` file with the placement of the JAR file executing it. This repository contains a `verification-rules.rules` file which is included by the main `inference-rules.rules` file. This means that the filepath listed under the `@include` directive by default forces the JAR file to be executed from the same directory. Executing the JAR file from another directory will therefore require updating the filepath in the `@include` directive of the `inference-rules.rules` file.

`OptimalConditions` are meant to be used for optimal ranges of `Property` values, so the solution currently only offers support for the `>, >=, <, <=` operators being used in constraints. Note that one may define multiple constraints per `OptimalCondition`, which will be used in conjunctions for the `Property` specified. Likewise, multiple `OptimalCondition` individuals defined for the same `Property` will have their constraints applied in conjunction (and must thus adhere to verification rules). We also offer support for disjoint ranges which users can specify with disjunctions, e.g., (Protege Manchester syntax) `hasValueConstraint exactly 1 (xsd:double[< "5.4"^^xsd:double] or xsd:double[> "10.8"^^xsd:double])`.

Users are also encouraged to add their own files through the included `user-rules.rules` file.

### Example

```
$ java -jar ruleless-digital-twins-inference-engine.jar ../Ontology/ruleless-digital-twins.ttl instance-model-1.ttl inference-rules.rules inf-out.ttl
```

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

## Running the Control Loop (SmartNode)
The codebase is a .NET 8 solution consisting of 4 projects: `Logic` (MAPE-K and models), `SensorActuatorImplementations` (for user-provided sensor/actuator implementations), `SmartNode` (startup project), and `TestProject` (unit tests). Users may choose between running the solution natively or containerized.

The control loop accepts 3 initial arguments and 2 options:
1. The filepath of the (inferred) instance model.
2. The filepath of the directory storing the FMU (simulation) models.
3. The filepath of the directory to store control loop cycle data (property values and chosen actuator states) in CSV files.
4. The simulation boolean option for whether to use the dummy environment or not (default is false).
5. The round number integer for the total number of MAPE-K cycle rounds to execute (default is 4 and unlimited is -1).

**NB: Depending on your platform, the filepaths in the 3 initial arguments might be represented differently than the current defaults. Feel free to add/remove `/../`s as you see fit.**

The Logic project provides interfaces in the `Logic.DeviceInterfaces` directory for users to implement when providing their own custom connections to sensors and actuators. It also provides the `IValueHandler` interface for user-provided implementations of logic handling various operations with specific OWL types. The solution contains the `DoubleValueHandler`, `IntValueHandler`, and `TimespanValueHandler` as example implementations in the `SensorActuatorImplementations` project. These are registered in the `Factory` in the `SmartNode` project, where the user is expected to register other custom implementations as well.

The solution currently runs based on the example 1 instance model, found in `instance-model-1.ttl` in the `models-and-rules` directory. Running this through the inference engine produces `inferred-model-1.ttl` which is used throughout the control loop. You may also use `inferred-model-2.ttl`, representing cyber components, but note that the simulation process has not been implemented for cyber components.

By default, the solution runs with a fake (dummy) environment as its twinning target, but users can easily add their own implementations of real devices via the `Factory` class. 

The instance model contains two `OptimalConditions` that are satisfied in the first cycle by the dummy values provided by dummy sensor implementations. Users are welcome to add their own or change the value to see the effects throughout the loop. There are many logging statements showing various stages of execution as well as the specific SPARQL queries and their results. At the end of each control loop cycle, the solution should print its chosen combination of actions to take, demonstrating what the DT's decision would be.

### Required Steps for Using Cyber TTs
In our solution, cyber TTs are treated similarly to physical TTs. This means they use instance models conformant to the same ontology and use user-provided implementations to connect to soft sensors and software for reconfiguration as well for providing possible values for ConfigurableParameters. This means that, much like for providing implementations for generating Actuator states, a user should provide implementations for generating values of ConfigurableParameters.

Cyber TT reconfiguration is currently not supported by our simulation system but is on the planned task list.