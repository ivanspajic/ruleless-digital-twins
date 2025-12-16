using Implementations.ValueHandlers;
using Logic.DeviceInterfaces;
using Logic.FactoryInterface;
using Logic.Mapek;
using Logic.Models.MapekModels;
using Logic.Models.OntologicalModels;
using Logic.ValueHandlerInterfaces;
using System.Diagnostics;
using System.Reflection;
using TestProject.Mocks;
using Xunit.Internal;

namespace TestProject
{
    internal class Factory : IFactory {
        private readonly Dictionary<string, IValueHandler> _valueHandlers = new() {
            { "http://www.w3.org/2001/XMLSchema#double", new DoubleValueHandler() },
            { "double", new DoubleValueHandler() }, // FIXME
            { "http://www.w3.org/2001/XMLSchema#int", new IntValueHandler() }
        };
        public IActuatorDevice GetActuatorDeviceImplementation(string actuatorName)
        {
            throw new NotImplementedException();
        }

        public ISensorDevice GetSensorDeviceImplementation(string sensorName, string procedureName)
        {
            throw new NotImplementedException();
        }

        public IValueHandler GetValueHandlerImplementation(string owlType) {
            if (_valueHandlers.TryGetValue(owlType, out IValueHandler? sensorValueHandler)) {
                return sensorValueHandler;
            }
            throw new Exception($"No implementation was found for Sensor value handler for OWL type {owlType}.");
        }
    }

    public class NordPoolTests {
        [Theory]
        [InlineData("nordpool-simple.ttl", "nordpool-out.ttl", 4)]
        //[InlineData("nordpool1.ttl", "nordpool1-out.ttl", 4)]
        public void Actions_and_OptimalConditions_for_plan_phase_same_as_expected(string model, string inferred, int lookAheadCycles) {
            var executingAssemblyPath = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
            var modelFilePath = Path.Combine(executingAssemblyPath!, $"..{Path.DirectorySeparatorChar}..{Path.DirectorySeparatorChar}..{Path.DirectorySeparatorChar}..{Path.DirectorySeparatorChar}..{Path.DirectorySeparatorChar}"
                                +$"models-and-rules{Path.DirectorySeparatorChar}{model}");
            modelFilePath = Path.GetFullPath(modelFilePath);
            var inferredFilePath = Path.Combine(executingAssemblyPath!, $"..{Path.DirectorySeparatorChar}..{Path.DirectorySeparatorChar}..{Path.DirectorySeparatorChar}..{Path.DirectorySeparatorChar}..{Path.DirectorySeparatorChar}"
                                +$"models-and-rules{Path.DirectorySeparatorChar}{inferred}");

            var mock = new ServiceProviderMock(modelFilePath, inferredFilePath, new Factory());
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

            var simulations = mapekPlan.GetSimulationsAndGenerateSimulationTree(lookAheadCycles, 0, simulationTree, false, true, new List<List<ActuationAction>>());

            // To produce the tree via the streaming (yield return) mechanism, we need to enumerate the simulation collection.
            simulations.ForEach(_ => { });

            Assert.Single(simulationTree.SimulationPaths);
            Assert.Equal(simulationTree.ChildrenCount, lookAheadCycles);
            mapekPlan.Simulate(simulations);
            var path = simulationTree.SimulationPaths.First();

            foreach (var s in path.Simulations)
            {
                Trace.WriteLine(string.Join(";", s.ActuationActions.Select(a => a.Name)));
            }
            // TODO: assert that in each simulated timepoint ElPriceNF = false.
        }   
    }
}