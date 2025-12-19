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

namespace TestProject
{
    public class IncubatorTests
    {
        private class MyMapekPlan : MapekPlan {
            public MyMapekPlan(IServiceProvider serviceProvider, bool logSimulations = false) : base(serviceProvider, logSimulations) { }
            protected override void InferActionCombinations() {
                // Call Java explicitly?
                if (true) {
                    base.InferActionCombinations();
                }
            }
        }

        [Theory]
        [InlineData("Incubator.py", "incubator.ttl", "incubator-out.ttl", 4)]
        public void Simulate(string? fromPython, string model, string inferred, int lookAheadCycles) {
            var executingAssemblyPath = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
            var modelFilePath = Path.Combine(executingAssemblyPath!, $"..{Path.DirectorySeparatorChar}..{Path.DirectorySeparatorChar}..{Path.DirectorySeparatorChar}..{Path.DirectorySeparatorChar}..{Path.DirectorySeparatorChar}"
                                + $"models-and-rules");
            var inferredFilePath = Path.Combine(executingAssemblyPath!, $"..{Path.DirectorySeparatorChar}..{Path.DirectorySeparatorChar}..{Path.DirectorySeparatorChar}..{Path.DirectorySeparatorChar}..{Path.DirectorySeparatorChar}"
                                + $"models-and-rules{Path.DirectorySeparatorChar}{inferred}");
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
                var outPath = Path.Combine(executingAssemblyPath!, $"..{Path.DirectorySeparatorChar}..{Path.DirectorySeparatorChar}..{Path.DirectorySeparatorChar}..{Path.DirectorySeparatorChar}..{Path.DirectorySeparatorChar}"
                                + $"models-and-rules{Path.DirectorySeparatorChar}{model}");
                outPath = Path.GetFullPath(outPath);
                File.WriteAllText(outPath, output);
                process.WaitForExit();
                Assert.Equal(0, process.ExitCode);
            }

            modelFilePath = Path.Combine(executingAssemblyPath!, $"..{Path.DirectorySeparatorChar}..{Path.DirectorySeparatorChar}..{Path.DirectorySeparatorChar}..{Path.DirectorySeparatorChar}..{Path.DirectorySeparatorChar}"
                                + $"models-and-rules{Path.DirectorySeparatorChar}{model}");
            modelFilePath = Path.GetFullPath(modelFilePath);

            var mock = new ServiceProviderMock(modelFilePath, inferredFilePath, new Factory());
            // TODO: not sure anymore if pulling it out was actually necessary in the end:
            mock.Add(typeof(IMapekKnowledge), new MapekKnowledge(mock));
            var mapekPlan = new MyMapekPlan(mock, false);

            var propertyCacheMock = new PropertyCache {
                ConfigurableParameters = new Dictionary<string, ConfigurableParameter>(),
                // TODO: Ideally we wouldn't need those, and either start with `undefined` or use the FMU's values.

                Properties = new Dictionary<string, Property> {
                    {
                        "http://www.semanticweb.org/vs/ontologies/2025/12/incubator#in_room_temperature",
                        new Property {
                            Name = "http://www.semanticweb.org/vs/ontologies/2025/12/incubator#in_room_temperature",
                            OwlType = "double",
                            Value = 10.0 // from FMU
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
            Assert.Equal(Math.Pow(2, lookAheadCycles), simulationTree.SimulationPaths.Count());
            Assert.Equal(30, simulationTree.ChildrenCount);
            var path = simulationTree.SimulationPaths.First();

            foreach (var s in path.Simulations)
            {
                Trace.WriteLine(string.Join(";", s.Actions.Select(a => a.Name)));
                Trace.WriteLine("Params: " + string.Join(";", s.InitializationActions.Select(a => a.Name).ToList()));
                Trace.WriteLine("Inputs: " + string.Join(";", s.Actions.Select(a => a.Name).ToList()));
            }
        }
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

    }
}