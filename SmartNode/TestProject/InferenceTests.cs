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
        // TODO: make this work in containers. xUnit allows for skippable tests depending on the platform, so we can have a similar one for containers.
        // This is really an integration test.
        [Theory(Explicit = true)]
        [MemberData(nameof(InferenceTestHelper.TestData), MemberType = typeof(InferenceTestHelper))]
        public void Correct_action_combinations_for_instance_model(string instanceModelFilename, IEnumerable<IEnumerable<ActuationAction>> expectedCombinations) {
            // Arrange
            var executingAssemblyPath = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);

            var workingDirectoryPath = Path.Combine(executingAssemblyPath!, "ModelsAndRules");
            var ontologyFilepath = Path.Combine(executingAssemblyPath!, "Ontology", "ruleless-digital-twins.ttl");
            var inferenceRulesFilepath = Path.Combine(executingAssemblyPath!, "ModelsAndRules", "inference-rules.rules");
            var instanceModelFilepath = Path.Combine(executingAssemblyPath!, "TestFiles", instanceModelFilename);
            var inferredInstanceModelFilepath = Path.Combine(executingAssemblyPath!, "TestFiles", instanceModelFilename.Split('.')[0] + "Inferred.ttl");

            var inferredModel = new Graph();
            var turtleParser = new TurtleParser();

            // Act
            ExecuteJarFile($"\"{Path.Combine(executingAssemblyPath!, "ModelsAndRules", "ruleless-digital-twins-inference-engine.jar")}\"",
                [$"\"{ontologyFilepath}\"", $"\"{instanceModelFilepath}\"", $"\"{inferenceRulesFilepath}\"", $"\"{inferredInstanceModelFilepath}\""],
                workingDirectoryPath);

            turtleParser.Load(inferredModel, inferredInstanceModelFilepath);

            var actualCombinations = GetActionCombinationsFromInferredModel(inferredModel);

            // Assert
            Assert.Equivalent(expectedCombinations, actualCombinations, true);
        }

        private static void ExecuteJarFile(string jarFilepath, string[] arguments, string workingDirectoryPath) {
            var processInfo = new ProcessStartInfo {
                FileName = "java",
                Arguments = $"-jar {jarFilepath} {string.Join(" ", arguments)}",
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true,
                WorkingDirectory = workingDirectoryPath
            };
            
            using var process = Process.Start(processInfo);

            process!.OutputDataReceived += (sender, e) => {
                Debug.WriteLine(e.Data);
            };

            process.ErrorDataReceived += (sender, e) => {
                Debug.WriteLine(e.Data);
            };

            Debug.WriteLine($"Process started with ID {process.Id}.");
            process.WaitForExit();

            if (process.ExitCode != 0) {
                throw new Exception($"The inference engine encountered an error. Process {process.Id} exited with code {process.ExitCode}.");
            }

            Debug.WriteLine($"Process {process.Id} exited with code {process.ExitCode}.");
        }

        private static List<List<ActuationAction>> GetActionCombinationsFromInferredModel(IGraph inferredModel) {
            var actionCombinations = new List<List<ActuationAction>>();

            var actionCombinationQuery = GetParameterizedStringQuery(@"SELECT ?actionCombination (GROUP_CONCAT(?action; SEPARATOR="" "") AS ?actions) WHERE {
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
                GROUP BY ?actionCombination");

            var actionCombinationQueryResult = (SparqlResultSet)inferredModel.ExecuteQuery(actionCombinationQuery);

            actionCombinationQueryResult.Results.ForEach(combinationResult => {
                var actions = combinationResult["actions"].ToString().Split('^')[0].Split(' ');

                var combination = new List<ActuationAction>();

                actions.ForEach(action => {
                    var actionQuery = GetParameterizedStringQuery(@"SELECT ?actuator ?actuatorState WHERE {
                        @action rdf:type meta:ActuationAction .
                        @action meta:hasActuator ?actuator .
                        @action meta:hasActuatorState ?actuatorState . }");

                    actionQuery.SetUri("action", new Uri(action));

                    var actionQueryResult = (SparqlResultSet)inferredModel.ExecuteQuery(actionQuery);

                    actionQueryResult.Results.ForEach(actionResult => {
                        var actuatorName = actionResult["actuator"].ToString();
                        var actuatorState = actionResult["actuatorState"].ToString().Split('^')[0];

                        combination.Add(new ActuationAction {
                            Name = action,
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

        private static SparqlParameterizedString GetParameterizedStringQuery(string queryString) {
            var dtPrefix = "meta";
            var dtUri = "http://www.semanticweb.org/ivans/ontologies/2025/ruleless-digital-twins/";
            var sosaPrefix = "sosa";
            var sosaUri = "http://www.w3.org/ns/sosa/";
            var ssnPrefix = "ssn";
            var ssnUri = "http://www.w3.org/ns/ssn/";
            var rdfPrefix = "rdf";
            var rdfUri = "http://www.w3.org/1999/02/22-rdf-syntax-ns#";
            var owlPrefix = "owl";
            var owlUri = "http://www.w3.org/2002/07/owl#";
            var xsdPrefix = "xsd";
            var xsdUri = "http://www.w3.org/2001/XMLSchema#";

            var query = new SparqlParameterizedString {
                CommandText = queryString
            };

            query.Namespaces.AddNamespace(dtPrefix, new Uri(dtUri));
            query.Namespaces.AddNamespace(sosaPrefix, new Uri(sosaUri));
            query.Namespaces.AddNamespace(ssnPrefix, new Uri(ssnUri));
            query.Namespaces.AddNamespace(rdfPrefix, new Uri(rdfUri));
            query.Namespaces.AddNamespace(owlPrefix, new Uri(owlUri));
            query.Namespaces.AddNamespace(xsdPrefix, new Uri(xsdUri));

            return query;
        }
    }
}
