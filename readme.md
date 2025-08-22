### Running the Inference Engine
The `dt-code-generation-inference-engine.jar` file should be executed from the console with 4 arguments provided:
1. The filepath of the ontology.
2. The filepath of the instance model (that uses the ontology).
3. The filepath of the inference rules. In case of multiple inference rules files, this filepath should be of the main file that includes the others.
4. The filepath of the inferred model.

If you are using multiple `.rules` files for inferencing, then you must make sure to match the `@include` directive filepaths in the main `.rules` file with the placement of the JAR file executing it. This repository contains a `validation-rules.rules` file which is included by the main `inference-rules.rules` file. This means that the filepath listed under the `@include` directive by default forces the JAR file to be executed from the same directory. Executing the JAR file from another directory will therefore require updating the filepath in the `@include` directive of the `inference-rules.rules` file.

## Docker-based example

The Dockerfile builds and runs the example inside the container. Note that `arm64` is currently not supported by one of the libraries that we depend on.
We use OpenModelica inside the container to compile the example FMU(s) into matching binaries.

```
% docker build -t smartnode -f SmartNode/SmartNode/Dockerfile SmartNode
...
=> => unpacking to docker.io/library/smartnode:latest
% docker run -v `pwd`/models-and-rules:/app/models smartnode /app/models/inferred-model-1.ttl
info: Logic.Mapek.MapekManager[0]
      Starting the MAPE-K loop.
...
```
