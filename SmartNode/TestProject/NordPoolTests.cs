using Logic.Mapek;
using Logic.Models.MapekModels;
using Logic.Models.OntologicalModels;
using System.Diagnostics;
using System.Reflection;
using TestProject.Mocks;

namespace TestProject
{
    public class NordPoolTests {
        [Theory]
        [InlineData("nordpool-simple.ttl", "nordpool-out.ttl", 4)]
        [InlineData("nordpool1.ttl", "nordpool1-out.ttl", 4)]
        public void Actions_and_OptimalConditions_for_plan_phase_same_as_expected(string model, string inferred, int simulationGranularity) {
            var executingAssemblyPath = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
            var modelFilePath = Path.Combine(executingAssemblyPath!, $"..{Path.DirectorySeparatorChar}..{Path.DirectorySeparatorChar}..{Path.DirectorySeparatorChar}..{Path.DirectorySeparatorChar}..{Path.DirectorySeparatorChar}"
                                +$"models-and-rules{Path.DirectorySeparatorChar}{model}");
            modelFilePath = Path.GetFullPath(modelFilePath);
            var inferredFilePath = Path.Combine(executingAssemblyPath!, $"..{Path.DirectorySeparatorChar}..{Path.DirectorySeparatorChar}..{Path.DirectorySeparatorChar}..{Path.DirectorySeparatorChar}..{Path.DirectorySeparatorChar}"
                                +$"models-and-rules{Path.DirectorySeparatorChar}{inferred}");

            var mock = new ServiceProviderMock(modelFilePath, inferredFilePath);
            // TODO: not sure anymore if pulling it out was actually necessary in the end:
            mock.Add(typeof(IMapekKnowledge), new MapekKnowledge(mock));
            var mapekPlan = new MapekPlan(mock, false) ;

            var propertyCacheMock = new PropertyCache {
                ConfigurableParameters = new Dictionary<string, ConfigurableParameter>(),
                // TODO: This test shouldn't need those I think:
                Properties = new Dictionary<string, Property> {
                    {
                        "http://www.semanticweb.org/vs/ontologies/2025/11/untitled-ontology-97#MeasuredOutputProperty",
                        new Property {
                            Name = "http://www.semanticweb.org/vs/ontologies/2025/11/untitled-ontology-97#MeasuredOutputProperty",
                            OwlType = "double",
                            Value = -1.02
                        }
                    },
                    {
                        "http://www.semanticweb.org/vs/ontologies/2025/11/untitled-ontology-97#DummyProperty",
                        new Property {
                            Name = "http://www.semanticweb.org/vs/ontologies/2025/11/untitled-ontology-97#DummyProperty",
                            OwlType = "double",
                            Value = -1.02
                        }
                    },
                    {
                        "http://www.semanticweb.org/vs/ontologies/2025/11/untitled-ontology-97#PriceMeasure",
                        new Property {
                            Name = "http://www.semanticweb.org/vs/ontologies/2025/11/untitled-ontology-97#PriceMeasure",
                            OwlType = "double",
                            Value = -1.02
                        }
                    },
                    {
                        "http://www.semanticweb.org/vs/ontologies/2025/11/untitled-ontology-97#price",
                        new Property {
                            Name = "http://www.semanticweb.org/vs/ontologies/2025/11/untitled-ontology-97#price",
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
            var simulations = mapekPlan.PlanAll(propertyCacheMock, simulationGranularity);
            // We're expecting a single tree...
            Assert.Single(simulationTree.SimulationPaths);
            // ... of length 4:
            Assert.Equal(simulationTree.ChildrenCount, simulationGranularity);

            var path = simulationTree.SimulationPaths.First();
            foreach (var s in path.Simulations) {
                Trace.WriteLine(string.Join(";", s.ActuationActions.Select(a => a.Name)));
            }
        }
    }
}