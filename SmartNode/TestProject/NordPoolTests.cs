using Logic.Mapek;
using Logic.Mapek.Comparers;
using Logic.Models.MapekModels;
using Logic.Models.OntologicalModels;
using System.Reflection;
using TestProject.Mocks;
using VDS.RDF;
using VDS.RDF.Parsing;
// using VDS.RDF.Query.Paths;

namespace TestProject
{
    public class NordPoolTests
    {
        [Fact]
        public void Actions_and_OptimalConditions_for_plan_phase_same_as_expected()
        {
            // Arrange
            var executingAssemblyPath = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
            var modelFilePath = Path.Combine(executingAssemblyPath!, $"..{Path.DirectorySeparatorChar}..{Path.DirectorySeparatorChar}..{Path.DirectorySeparatorChar}..{Path.DirectorySeparatorChar}..{Path.DirectorySeparatorChar}models-and-rules{Path.DirectorySeparatorChar}nordpool-out.ttl");
            modelFilePath = Path.GetFullPath(modelFilePath);

            var instanceModel = new Graph();
            var turtleParser = new TurtleParser();
            turtleParser.Load(instanceModel, modelFilePath);

            var mapekPlan = new MapekPlan(new ServiceProviderMock());

            var simulationGranularity = 4;

            var propertyCacheMock = new PropertyCache
            {
                ConfigurableParameters = new Dictionary<string, ConfigurableParameter>(),
                Properties = new Dictionary<string, Property>
                {
                    {
                        "http://www.semanticweb.org/vs/ontologies/2025/11/untitled-ontology-97#MeasuredOutputProperty",
                        new Property
                        {
                            Name = "http://www.semanticweb.org/vs/ontologies/2025/11/untitled-ontology-97#MeasuredOutputProperty",
                            OwlType = "double",
                            Value = -1.02
                        }
                    },
                    {
                        "http://www.semanticweb.org/vs/ontologies/2025/11/untitled-ontology-97#DummyProperty",
                        new Property
                        {
                            Name = "http://www.semanticweb.org/vs/ontologies/2025/11/untitled-ontology-97#DummyProperty",
                            OwlType = "double",
                            Value = -1.02
                        }
                    },
                    {
                        "http://www.semanticweb.org/vs/ontologies/2025/11/untitled-ontology-97#PriceMeasure",
                        new Property
                        {
                            Name = "http://www.semanticweb.org/vs/ontologies/2025/11/untitled-ontology-97#PriceMeasure",
                            OwlType = "double",
                            Value = -1.02
                        }
                    },
                    {
                        "http://www.semanticweb.org/vs/ontologies/2025/11/untitled-ontology-97#price",
                        new Property
                        {
                            Name = "http://www.semanticweb.org/vs/ontologies/2025/11/untitled-ontology-97#price",
                            OwlType = "double",
                            Value = -1.02
                        }
                    },
                    {
                        "http://www.semanticweb.org/ivans/ontologies/2025/instance-model-1#HumiditySensorMeasuredHumidity",
                        new Property
                        {
                            Name = "http://www.semanticweb.org/ivans/ontologies/2025/instance-model-1#HumiditySensorMeasuredHumidity",
                            OwlType = "double",
                            Value = -1.02
                        }
                    }
                }
            };

            var simulationTree = new SimulationTreeNode {
                Simulation = new Simulation(propertyCacheMock),
                Children = []
            };
            // Tree gets updated after next call:
            var simulations = mapekPlan.GetSimulationsAndGenerateSimulationTree(simulationGranularity, 0, simulationTree, false, true, new List<List<ActuationAction>>());            
            Assert.Equal(simulationTree.ChildrenCount, simulationGranularity);
        }
    }
}