using Implementations.ValueHandlers;
using Logic.TTComponentInterfaces;
using Logic.FactoryInterface;
using Logic.Mapek;
using Logic.Models.MapekModels;
using Logic.Models.OntologicalModels;
using Logic.ValueHandlerInterfaces;
using System.Diagnostics;
using System.Reflection;
using TestProject.Mocks;
using Logic.Models.DatabaseModels;

namespace TestProject
{
    internal class Factory : IFactory {
        private readonly Dictionary<string, IValueHandler> _valueHandlers = new() {
            { "http://www.w3.org/2001/XMLSchema#double", new DoubleValueHandler() },
            { "double", new DoubleValueHandler() }, // FIXME
            { "boolean", new BooleanValueHandler() },
            { "string", new StringValueHandler() },
            { "http://www.w3.org/2001/XMLSchema#string", new StringValueHandler() },
            { "http://www.w3.org/2001/XMLSchema#int", new IntValueHandler() }
        };
        public IActuator GetActuatorDeviceImplementation(string actuatorName)
        {
            throw new NotImplementedException();
        }

        public IConfigurableParameter GetConfigurableParameterImplementation(string configurableParameterName) {
            throw new NotImplementedException();
        }

        public ISensor GetSensorDeviceImplementation(string sensorName, string procedureName)
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

    class MyMapekPlan : MapekPlan {
        public MyMapekPlan(IServiceProvider serviceProvider, bool logSimulations = false) : base(serviceProvider) {}
        protected override void InferActionCombinations() {
            // Call Java explicitly?
            if (true) {
                base.InferActionCombinations();
            }
        }
    }

    public class NordPoolTests {
        [Theory]
        [InlineData(null, "nordpool-simple.ttl", "nordpool-out.ttl", 4)]
        [InlineData("SimpleNordpool.py", "nordpool1.ttl", "nordpool1-out.ttl", 4)]
        public void Smallest_model_builds_tree_and_simulates(string? fromPython, string model, string inferred, int lookAheadCycles) {
            var rootDirectory = Directory.GetParent(Assembly.GetExecutingAssembly().Location)!.Parent!.Parent!.Parent!.Parent!.Parent!.FullName;
            var modelFilePath = Path.Combine(rootDirectory, "models-and-rules");
            var inferredFilePath = Path.Combine(rootDirectory, $"models-and-rules{Path.DirectorySeparatorChar}{inferred}");
            // TODO: Review why file must exist if we're going to overwrite it anyway.
            if (!File.Exists(inferredFilePath)) {
                File.Create(inferredFilePath).Close();
            }

            if (fromPython != null) {
                var processInfo = new ProcessStartInfo {
                    FileName = "python3",
                    Arguments = $"\"{fromPython}\"",
                    RedirectStandardOutput = true,
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    WorkingDirectory = modelFilePath
                };
                using var process = Process.Start(processInfo);
                Debug.Assert(process != null, "Process failed to start.");
                StreamReader reader = process.StandardOutput;
                string output = reader.ReadToEnd();
                var outPath = Path.Combine(rootDirectory, $"models-and-rules{Path.DirectorySeparatorChar}{model}");
                outPath = Path.GetFullPath(outPath);
                File.WriteAllText(outPath, output);
                process.WaitForExit();
                Assert.Equal(0, process.ExitCode);
            }

            modelFilePath = Path.Combine(rootDirectory, $"models-and-rules{Path.DirectorySeparatorChar}{model}");
            modelFilePath = Path.GetFullPath(modelFilePath);

            var mock = new ServiceProviderMock(new Factory());
            mock.Add(new FilepathArguments {
                InstanceModelFilepath = modelFilePath,
                InferredModelFilepath = inferredFilePath,
                InferenceEngineFilepath = Path.Combine(rootDirectory, "models-and-rules", "ruleless-digital-twins-inference-engine.jar"),
                InferenceRulesFilepath = Path.Combine(rootDirectory, "models-and-rules", "inference-rules.rules"),
                OntologyFilepath = Path.Combine(rootDirectory, "ontology", "ruleless-digital-twins.ttl"),
                DataDirectory = Path.Combine(rootDirectory, "state-data"),
                FmuDirectory = Path.Combine(rootDirectory, "SmartNode", "Implementations", "FMUs")
            });
            mock.Add(new CoordinatorSettings {
                LookAheadMapekCycles = 4,
                MaximumMapekRounds = 4,
                StartInReactiveMode = false,
                SimulationDurationSeconds = 10,
                UseSimulatedEnvironment = true
            });
            // TODO: not sure anymore if pulling it out was actually necessary in the end:
            mock.Add(new MapekKnowledge(mock));
            var mapekPlan = new MyMapekPlan(mock, false);

            var propertyCacheMock = new PropertyCache {
                ConfigurableParameters = new Dictionary<string, ConfigurableParameter>(),
                // TODO: Ideally we wouldn't need those, and either start with `undefined` or use the FMU's values.

                Properties = new Dictionary<string, Property> {
                    {
                        "http://www.semanticweb.org/vs/ontologies/2025/11/untitled-ontology-97#DummyProperty",
                        new Property {
                            Name = "http://www.semanticweb.org/vs/ontologies/2025/11/untitled-ontology-97#DummyProperty",
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
                    },
                    {
                        "http://www.semanticweb.org/vs/ontologies/2025/11/untitled-ontology-97#notFound",
                        new Property {
                            Name = "http://www.semanticweb.org/vs/ontologies/2025/11/untitled-ontology-97#notFound",
                            OwlType = "boolean",
                            Value = true
                        }
                    }
                }
            };

            var simulationTree = new SimulationTreeNode {
                NodeItem = new Simulation(propertyCacheMock),
                Children = []
            };

            var simulations = mapekPlan.GetSimulationsAndGenerateSimulationTree(lookAheadCycles, 0, simulationTree, false, true, new List<List<Logic.Models.OntologicalModels.Action>>(), propertyCacheMock);

            mapekPlan.Simulate(simulations, []);

            // Only valid AFTER focing evaluation through simulation:
            Assert.Single(simulationTree.SimulationPaths);
            Assert.Equal(simulationTree.ChildrenCount, lookAheadCycles);
            var path = simulationTree.SimulationPaths.First();

            foreach (var s in path.Simulations)
            {
                Trace.WriteLine(string.Join(";", s.Actions.Select(a => a.Name)));
                Trace.WriteLine("Params: " + string.Join(";", s.InitializationActions.Select(a => a.Name).ToList()));
                Trace.WriteLine("Inputs: " + string.Join(";", s.Actions.Select(a => a.Name).ToList()));
            }
            // TODO: assert that in each simulated timepoint ElPriceNF = false.
        }   
    }
}