### Running the Inference Engine
The dt-code-generation-inference-engine.jar file should be executed from the console with 4 arguments provided:
1. The filepath of the ontology.
2. The filepath of the instance model (that uses the ontology).
3. The filepath of the inference rules.
4. The filepath of the file to be inferred.

If you are using multiple .rules files for inferencing, then you must make sure to match the @include directive filepaths in the main .rules file with the placement of the JAR file executing it. This repository contains a validation-rules.rules file which is included by the main inference-rules.rules file. This means that the filepath listed under the @include directive by default forces the JAR file to be executed from the same directory. Executing the JAR file from another directory will therefore require updating the filepath in the @include directive of the inference-rules.rules file.