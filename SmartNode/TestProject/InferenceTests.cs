using Logic.Mapek;
using Logic.Models.OntologicalModels;
using System.Diagnostics;
using System.Reflection;
using TestProject.Utilities;
using VDS.RDF;
using VDS.RDF.Parsing;
using VDS.RDF.Query;
using Xunit.Internal;

namespace TestProject
{
    public class InferenceTests
    {
        [Theory]
        [MemberData(nameof(InferenceTestHelper.TestData), MemberType = typeof(InferenceTestHelper))]
        public void Correct_action_combinations_for_instance_model(string instanceModelFilename, IEnumerable<IEnumerable<ActuationAction>> expectedCombinations) {
            // Arrange
            var executingAssemblyPath = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
            var workingDirectoryPath = Path.Combine(InferenceTestHelper.SolutionRootDirectory, "models-and-rules");
            var ontologyFilepath = Path.Combine(InferenceTestHelper.SolutionRootDirectory, "Ontology", "ruleless-digital-twins.ttl");
            var inferenceRulesFilepath = Path.Combine(InferenceTestHelper.SolutionRootDirectory, "models-and-rules", "inference-rules.rules");
            var instanceModelFilepath = Path.Combine(InferenceTestHelper.TestFileDirectory, instanceModelFilename);
            var inferredInstanceModelFilepath = Path.Combine(InferenceTestHelper.TestFileDirectory, instanceModelFilename.Split('.')[0] + "Inferred.ttl");

            var inferredModel = new Graph();
            var turtleParser = new TurtleParser();

            // Act
            ExecuteJarFile($"\"{Path.Combine(InferenceTestHelper.SolutionRootDirectory, "models-and-rules", "ruleless-digital-twins-inference-engine.jar")}\"",
                [$"\"{ontologyFilepath}\"", $"\"{instanceModelFilepath}\"", $"\"{inferenceRulesFilepath}\"", $"\"{inferredInstanceModelFilepath}\""],
                workingDirectoryPath);

            turtleParser.Load(inferredModel, inferredInstanceModelFilepath);

            var actualCombinations = GetActionCombinationsFromInferredModel(inferredModel);

            // Assert
            Assert.Equivalent(expectedCombinations, actualCombinations, true);
        }

        private static void ExecuteJarFile(string jarFilepath, string[] arguments, string workingDirectoryPath) {
            var process = new Process();

            // Assumes "java" is added to the PATH and can be invoked from anywhere.
            process.StartInfo.FileName = "java";
            process.StartInfo.Arguments = $"-jar {jarFilepath} {string.Join(" ", arguments)}";
            process.StartInfo.RedirectStandardOutput = true;
            process.StartInfo.RedirectStandardError = true;
            process.StartInfo.UseShellExecute = false;
            process.StartInfo.CreateNoWindow = false;
            process.StartInfo.WorkingDirectory = workingDirectoryPath;

            process.OutputDataReceived += (sender, e) => {
                Debug.WriteLine(e.ToString());
            };

            process.ErrorDataReceived += (sender, e) => {
                Debug.WriteLine(e.ToString());
            };

            process.Start();
            process.WaitForExit();

            
        }

        private static List<List<ActuationAction>> GetActionCombinationsFromInferredModel(IGraph inferredModel) {
            var actionCombinations = new List<List<ActuationAction>>();

            var actionCombinationQuery = MapekUtilities.GetParameterizedStringQuery();

            actionCombinationQuery.CommandText = @"SELECT ?actionCombination (GROUP_CONCAT(?action; SEPARATOR="" "") AS ?actions) WHERE {
	                ?actionCombination rdf:type meta:ActionCombination .
	                FILTER NOT EXISTS {
		                {
			                ?actionCombination rdf:comment ""duplicate""^^<http://www.w3.org/2001/XMLSchema#string> .
		                }
		                UNION
		                {
			                ?actionCombination rdf:comment ""not final""^^<http://www.w3.org/2001/XMLSchema#string> .
		                }
	                }
	                ?actionCombination meta:hasActions ?actionList .
	                ?actionList rdf:rest*/rdf:first ?action . }
                GROUP BY ?actionCombination";

            var actionCombinationQueryResult = (SparqlResultSet)inferredModel.ExecuteQuery(actionCombinationQuery);

            actionCombinationQueryResult.Results.ForEach(combinationResult => {
                var actions = combinationResult["actions"].ToString().Split(' ');

                var combination = new List<ActuationAction>();

                actions.ForEach(action => {
                    var actionQuery = MapekUtilities.GetParameterizedStringQuery();

                    actionQuery.CommandText = @"SELECT ?action ?actuator ?actuatorState WHERE {
                        ?action rdf:type meta:ActuationAction .
                        ?action meta:hasActuator ?actuator .
                        ?action meta:hasActuatorState ?actuatorState . }";

                    var actionQueryResult = (SparqlResultSet)inferredModel.ExecuteQuery(actionQuery);

                    actionQueryResult.Results.ForEach(actionResult => {
                        var actionName = actionResult["action"].ToString();
                        var actuatorName = actionResult["actuator"].ToString();
                        var actuatorState = actionResult["actuatorState"].ToString().Split('^')[0];

                        combination.Add(new ActuationAction {
                            Name = actionName,
                            Actuator = new Actuator {
                                Name = actuatorName
                            },
                            NewStateValue = actuatorState
                        });
                    });
                });

                actionCombinations.Add(combination);
            });

            return actionCombinations;
        }
    }
}
