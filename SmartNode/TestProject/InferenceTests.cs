using Logic.Mapek;
using Logic.Models.OntologicalModels;
using System.Diagnostics;
using System.Reflection;
using TestProject.Utilities;
using VDS.RDF;
using VDS.RDF.Parsing;
using VDS.RDF.Query;

namespace TestProject
{
    public class InferenceTests
    {
        [Theory]
        [MemberData(nameof(InferenceTestHelper.TestData), MemberType = typeof(InferenceTestHelper))]
        public void Correct_action_combinations_for_instance_model(string instanceModelFilepath, IEnumerable<IEnumerable<ActuationAction>> combinations) {
            // Arrange
            var executingAssemblyPath = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
            var workingDirectoryPath = $"{executingAssemblyPath}/../../models-and-rules/";
            var ontologyFilepath = $"{executingAssemblyPath}/../../Ontology/ruleless-digital-twins.ttl";
            var inferenceRulesFilepath = $"{executingAssemblyPath}/../../models-and-rules/inference-rules.rules";
            var inferredInstanceModelFilepath = Path.Combine("inferred_", instanceModelFilepath);

            var inferredModel = new Graph();
            var turtleParser = new TurtleParser();

            // Act
            ExecuteJarFile("../../../models-and-rules/ruleless-digital-twins-inference-engine.jar",
                [$"{ontologyFilepath}", $"{instanceModelFilepath}", $"{inferenceRulesFilepath}", $"{inferredInstanceModelFilepath}"],
                workingDirectoryPath);

            turtleParser.Load(inferredModel, inferredInstanceModelFilepath);

            var actionCombinations = GetActionCombinationsFromInferredModel(inferredModel);

            // Assert

        }

        private static void ExecuteJarFile(string jarFilepath, string[] arguments, string workingDirectoryPath) {
            var process = new Process();

            process.StartInfo.FileName = "java";
            process.StartInfo.Arguments = $"-jar {jarFilepath} {string.Join(" ", arguments)}";
            process.StartInfo.RedirectStandardOutput = true;
            process.StartInfo.RedirectStandardError = true;
            process.StartInfo.UseShellExecute = true;
            process.StartInfo.CreateNoWindow = true;
            process.StartInfo.WorkingDirectory = workingDirectoryPath;

            process.Start();

            string standardOutput = process.StandardOutput.ReadToEnd();
            string standardError = process.StandardError.ReadToEnd();

            process.WaitForExit();
        }

        private static List<List<ActuationAction>> GetActionCombinationsFromInferredModel(IGraph inferredModel) {
            var actionCombinations = new List<List<ActuationAction>>();

            var actuationQuery = MapekUtilities.GetParameterizedStringQuery();

            actuationQuery.CommandText = @"SELECT ?actionCombination (GROUP_CONCAT(?action; SEPARATOR="" "") AS ?actions) WHERE {
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

            var actuationQueryResult = (SparqlResultSet)inferredModel.ExecuteQuery(actuationQuery);

            foreach (var result in actuationQueryResult.Results) {
                
            }

            return actionCombinations;
        }
    }
}
