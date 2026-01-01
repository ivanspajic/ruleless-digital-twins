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
using Implementations.Sensors;
using Logic.Models.DatabaseModels;

namespace TestProject
{
    public class IncubatorTests
    {
        static bool runInference = false; // `false` can be overriden by logic below.
        static IncubatorAdapter i;

        private class MyMapekPlan : MapekPlan {

            public MyMapekPlan(IServiceProvider serviceProvider, bool logSimulations = false) : base(serviceProvider) {
                // TODO: If the simulation runs overboard and the FMU throws LOG_ASSERT,
                // FMI calls with fail ungracefully.
                //MaximumSimulationTimeSeconds = 10;
            }

            protected override void InferActionCombinations() {
                // Call Java explicitly?
                if (IncubatorTests.runInference) {
                    base.InferActionCombinations();
                }
            }
        }

        [Theory]
        [InlineData("Incubator.py", "incubator.ttl", "incubator-out.ttl", 4)]
        public void Simulate(string? fromPython, string model, string inferred, int lookAheadCycles)
        {
            var rootDirectory = Directory.GetParent(Assembly.GetExecutingAssembly().Location)!.Parent!.Parent!.Parent!.Parent!.Parent!.FullName;
            var modelFilePath = Path.Combine(rootDirectory, "models-and-rules");
            var inferredFilePath = Path.Combine(rootDirectory, $"models-and-rules{Path.DirectorySeparatorChar}{inferred}");
            // TODO: Review why file must exist if we're going to overwrite it anyway.
            if (!File.Exists(inferredFilePath))
            {
                File.Create(inferredFilePath).Close();
            }

            GenerateFromPython(fromPython, model, rootDirectory, modelFilePath);

            modelFilePath = Path.Combine(rootDirectory, $"models-and-rules{Path.DirectorySeparatorChar}{model}");
            modelFilePath = Path.GetFullPath(modelFilePath);

            var mock = new ServiceProviderMock(new Factory());
            // TODO: not sure anymore if pulling it out was actually necessary in the end:
            mock.Add(typeof(FilepathArguments), new FilepathArguments {
                InstanceModelFilepath = modelFilePath,
                InferredModelFilepath = inferredFilePath,
                InferenceEngineFilepath = Path.Combine(rootDirectory, "models-and-rules", "ruleless-digital-twins-inference-engine.jar"),
                InferenceRulesFilepath = Path.Combine(rootDirectory, "models-and-rules", "inference-rules.rules"),
                OntologyFilepath = Path.Combine(rootDirectory, "ontology", "ruleless-digital-twins.ttl"),
                DataDirectory = Path.Combine(rootDirectory, "state-data"),
                FmuDirectory = Path.Combine(rootDirectory, "SmartNode", "Implementations", "FMUs")
            });
            mock.Add(typeof(CoordinatorSettings), new CoordinatorSettings {
                LookAheadMapekCycles = 4,
                MaximumMapekRounds = 4,
                StartInReactiveMode = false,
                SimulationDurationSeconds = 10,
                UseSimulatedEnvironment = true
            });
            var mpk = new MapekKnowledge(mock);
            mock.Add(typeof(IMapekKnowledge), mpk);
            var mapekPlan = new MyMapekPlan(mock, false);

            // TODO: Prototype populate cache from FMU.
            // If we're going to do this, we have to check that we correctly override with values from model.
            var fmu = Femyou.Model.Load("../../../../Implementations/FMUs/Source/au_incubator.fmu"); // TODO: grab from model
            var (SvType, SvValue) = fmu.Variables["G_box"]!.StartValue;
            Assert.Equal("Real", SvType);
            double gbox = double.Parse(SvValue);
            fmu.Dispose(); // Don't forget this or you'll get segfaults when loading the FMU "again" later.
            // END Prototype

            var propertyCacheMock = new PropertyCache
            {
                ConfigurableParameters = new Dictionary<string, ConfigurableParameter>(),
                // TODO: Ideally we wouldn't need those, and either start with `undefined` or use the FMU's values.

                Properties = new Dictionary<string, Property> {
                    {
                        "http://www.semanticweb.org/vs/ontologies/2025/12/incubator#in_room_temperature",
                        new Property {
                            Name = "http://www.semanticweb.org/vs/ontologies/2025/12/incubator#in_room_temperature",
                            OwlType = "double",
                            Value = 10.0
                        }
                    },
                   {
                        "http://www.semanticweb.org/vs/ontologies/2025/12/incubator#T",
                        new Property {
                            Name = "http://www.semanticweb.org/vs/ontologies/2025/12/incubator#T",
                            OwlType = "double",
                            Value = 10.0
                        }
                    },
                   {
                        "http://www.semanticweb.org/vs/ontologies/2025/12/incubator#T_heater",
                        new Property {
                            Name = "http://www.semanticweb.org/vs/ontologies/2025/12/incubator#T_heater",
                            OwlType = "double",
                            Value = 10.0
                        }
                    },
                   {
                        "http://www.semanticweb.org/vs/ontologies/2025/12/incubator#G_box",
                        new Property {
                            Name = "http://www.semanticweb.org/vs/ontologies/2025/12/incubator#G_box",
                            OwlType = "double",
                            Value = gbox
                        }
                    }
                }
            };

            mpk.Validate(propertyCacheMock);

            // TODO: Assert that there's at least one actuator that's not a parameter.

            var simulationTree = new SimulationTreeNode
            {
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

        [Theory]
        [InlineData("Incubator.py", "incubator.ttl", "incubator-out.ttl", 4)]
        public async Task FetchFromAMQ(string? fromPython, string model, string inferred, int lookAheadCycles) {
            var rootDirectory = Directory.GetParent(Assembly.GetExecutingAssembly().Location)!.Parent!.Parent!.Parent!.Parent!.Parent!.FullName;
            var modelFilePath = Path.Combine(rootDirectory, "models-and-rules");
            var inferredFilePath = Path.Combine(rootDirectory, $"models-and-rules{Path.DirectorySeparatorChar}{inferred}");
            // TODO: Review why file must exist if we're going to overwrite it anyway.
            if (!File.Exists(inferredFilePath)) {
                File.Create(inferredFilePath).Close();
            }

            GenerateFromPython(fromPython, model, rootDirectory, modelFilePath);

            modelFilePath = Path.Combine(rootDirectory, $"models-and-rules{Path.DirectorySeparatorChar}{model}");
            modelFilePath = Path.GetFullPath(modelFilePath);

            var mock = new ServiceProviderMock(new Factory());
            // TODO: not sure anymore if pulling it out was actually necessary in the end:
            var filepathArguments = new FilepathArguments {
                InstanceModelFilepath = modelFilePath,
                InferredModelFilepath = inferredFilePath,
                InferenceEngineFilepath = Path.Combine(rootDirectory, "models-and-rules", "ruleless-digital-twins-inference-engine.jar"),
                InferenceRulesFilepath = Path.Combine(rootDirectory, "models-and-rules", "inference-rules.rules"),
                OntologyFilepath = Path.Combine(rootDirectory, "ontology", "ruleless-digital-twins.ttl"),
                DataDirectory = Path.Combine(rootDirectory, "state-data"),
                FmuDirectory = Path.Combine(rootDirectory, "SmartNode", "Implementations", "FMUs")
            };
            mock.Add(typeof(FilepathArguments), filepathArguments);
            mock.Add(typeof(CoordinatorSettings), new CoordinatorSettings {
                LookAheadMapekCycles = 4,
                MaximumMapekRounds = 4,
                StartInReactiveMode = false,
                SimulationDurationSeconds = 10,
                UseSimulatedEnvironment = true
            });
            var mpk = new MapekKnowledge(mock);
            mock.Add(typeof(IMapekKnowledge), mpk);
            var mapekPlan = new MyMapekPlan(mock, false);

            // TODO: Prototype populate cache from FMU.
            // If we're going to do this, we have to check that we correctly override with values from model.
            var fmu = Femyou.Model.Load("../../../../Implementations/FMUs/au_incubator.fmu"); // TODO: grab from model
            var (SvType, SvValue) = fmu.Variables["G_box"]!.StartValue;
            Assert.Equal("Real", SvType);
            double gbox = double.Parse(SvValue);
            fmu.Dispose(); // Don't forget this or you'll get segfaults when loading the FMU "again" later.
            // END Prototype

            // IP is coming from "docket network create // inspect -> rabbitmq-ip"
            i = new IncubatorAdapter("172.20.0.2", TestContext.Current.CancellationToken);
            await i.Connect();
            var consumerTag = await i.Setup();
            IncubatorFields? myData = null;


            var monitor = new MapekMonitor(mock);
            var cache = monitor.Monitor();

            var simulationTree = new SimulationTreeNode
            {
                NodeItem = new Simulation(cache.PropertyCache),
                Children = []
            };

            var simulations = mapekPlan.GetSimulationsAndGenerateSimulationTree(lookAheadCycles, 0, simulationTree, false, true, new List<List<Logic.Models.OntologicalModels.Action>>(), cache.PropertyCache);

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

        private static void GenerateFromPython(string? fromPython, string model, string executingAssemblyPath, string modelFilePath) {
            if (fromPython != null) {
                var outPath = Path.Combine(executingAssemblyPath!, $"models-and-rules{Path.DirectorySeparatorChar}{model}");
                outPath = Path.GetFullPath(outPath);
                Assert.True(File.Exists(Path.Combine(modelFilePath, fromPython)));
                Assert.True(File.Exists(Path.Combine(modelFilePath, "RDTBindings.py")));
                if (runInference || !File.Exists(outPath) || File.GetLastWriteTime(Path.Combine(modelFilePath, fromPython)) > File.GetLastWriteTime(outPath) || File.GetLastWriteTime(Path.Combine(modelFilePath, "RDTBindings.py")) > File.GetLastWriteTime(outPath)) {
                    runInference = true;
                    Trace.WriteLine("Regenerating model...");
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
                    File.WriteAllText(outPath, output);
                    process.WaitForExit();
                    Assert.Equal(0, process.ExitCode);
                }
            }
        }

        internal class Factory : IFactory
        {
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

            public IConfigurableParameter GetConfigurableParameterImplementation(string configurableParameterName)
            {
                throw new NotImplementedException();
            }

            class AMQSensor(string sensorName, string procedureName) : ISensor
            {
                public string SensorName { get; private init; } = sensorName;

                public string ProcedureName { get; private init; } = procedureName;

                public object ObservePropertyValue(params object[] inputProperties)
                {
                    // Console.WriteLine("Observing AMQ Sensor Value: " + inputProperties[0]);
                    IncubatorFields? myData = null;
                    Monitor.Enter(i);
                    myData = i.Data;
                    Monitor.Exit(i);
                    Debug.Assert(myData != null, "No data received from Incubator AMQP.");
                    return myData.average_temperature;
                }
            }

            public ISensor GetSensorDeviceImplementation(string sensorName, string procedureName)
            {
                if (sensorName == "http://www.semanticweb.org/vs/ontologies/2025/12/incubator#TempSensor")
                {
                    if (procedureName == "http://www.semanticweb.org/vs/ontologies/2025/12/incubator#TempProcedure")
                    {
                        return new AMQSensor(sensorName, procedureName);
                    }
                }
                throw new NotImplementedException($"{sensorName}/{procedureName}");
            }

            public IValueHandler GetValueHandlerImplementation(string owlType)
            {
                if (_valueHandlers.TryGetValue(owlType, out IValueHandler? sensorValueHandler))
                {
                    return sensorValueHandler;
                }
                throw new Exception($"No implementation was found for Sensor value handler for OWL type {owlType}.");
            }
        }

    }
}