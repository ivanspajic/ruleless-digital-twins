using Implementations.ValueHandlers;
using Implementations.Sensors;
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
        static bool runInference = false; // `false` can be overriden by logic below.
        // IP is coming from "docket network create // inspect" -> rabbitmq-ip or variations thereof:
        static IncubatorAdapter i = new("172.20.0.2", TestContext.Current.CancellationToken);
        static Factory.AMQSensor AMQTempSensor = new("http://www.semanticweb.org/vs/ontologies/2025/12/incubator#TempSensor", "http://www.semanticweb.org/vs/ontologies/2025/12/incubator#TempProcedure", ((d) => d.average_temperature));

        private class MyMapekPlan : MapekPlan {

            public MyMapekPlan(IServiceProvider serviceProvider, bool logSimulations = false) : base(serviceProvider) {}

            protected override void InferActionCombinations() {
                // Call Java explicitly?
                if (IncubatorTests.runInference) {
                    base.InferActionCombinations();
                }
            }
        }

        [Theory]
        [InlineData("Incubator.py", "incubator.ttl", "incubator-out.ttl", 4)]
        public void SimulateFMUOnly(string fromPython, string model, string inferred, int lookAheadCycles)
        {
            SetupFiles(fromPython, model, inferred, out ServiceProviderMock mock, out FilepathArguments filepathArguments, out MapekKnowledge mapekKnowledge, out MyMapekPlan mapekPlan);

            // TODO: Prototype populate cache from FMU.
            // If we're going to do this, we have to check that we correctly override with values from model.
            var fmu = Femyou.Model.Load(Path.Combine(filepathArguments.FmuDirectory, "au_incubator.fmu")); // TODO: grab from model
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

            mapekKnowledge.Validate(propertyCacheMock);

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
        public async Task SimulateFromAMQ(string fromPython, string model, string inferred, int lookAheadCycles)
        {
            SetupFiles(fromPython, model, inferred, out ServiceProviderMock mock, out FilepathArguments filepathArguments, out MapekKnowledge mapekKnowledge, out MyMapekPlan mapekPlan);

            await i.Connect();
            var consumerTag = await i.Setup();
            Thread.Sleep(3); // Let's get a value.

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

        private static void SetupFiles(string fromPython, string model, string inferred, out ServiceProviderMock mock, out FilepathArguments filepathArguments, out MapekKnowledge mapekKnowledge, out MyMapekPlan mapekPlan)
        {
            var rootDirectory = Directory.GetParent(Assembly.GetExecutingAssembly().Location)!.Parent!.Parent!.Parent!.Parent!.Parent!.FullName;
            var modelDirPath = Path.Combine(rootDirectory, "models-and-rules");
            var inferredFilePath = Path.Combine(modelDirPath, inferred);
            var modelFilePath = Path.Combine(modelDirPath, model);
            modelFilePath = Path.GetFullPath(modelFilePath);
            GenerateFromPython(fromPython, modelFilePath, rootDirectory);
            // TODO: Review why file must exist if we're going to overwrite it anyway.
            if (!File.Exists(inferredFilePath)) {
                File.Create(inferredFilePath).Close();
                runInference = true;
            } else {
                DateTime x,y;
                runInference = !((x = File.GetLastWriteTime(inferredFilePath)) > (y = File.GetLastWriteTime(modelFilePath)));
            }

            mock = new ServiceProviderMock(new Factory());
            filepathArguments = new FilepathArguments
            {
                InstanceModelFilepath = modelFilePath,
                InferredModelFilepath = inferredFilePath,
                InferenceEngineFilepath = Path.Combine(rootDirectory, "models-and-rules", "ruleless-digital-twins-inference-engine.jar"),
                InferenceRulesFilepath = Path.Combine(rootDirectory, "models-and-rules", "inference-rules.rules"),
                OntologyFilepath = Path.Combine(rootDirectory, "ontology", "ruleless-digital-twins.ttl"),
                DataDirectory = Path.Combine(rootDirectory, "state-data"),
                FmuDirectory = Path.Combine(rootDirectory, "SmartNode", "Implementations", "FMUs")
            };
            mock.Add(typeof(FilepathArguments), filepathArguments);
            mock.Add(typeof(CoordinatorSettings), new CoordinatorSettings
            {
                LookAheadMapekCycles = 4,
                MaximumMapekRounds = 4,
                ReactiveMode = false,
                SimulationTimeSeconds = 10,
                UseSimulatedEnvironment = true
            });
            mapekKnowledge = new MapekKnowledge(mock);
            mock.Add(typeof(IMapekKnowledge), mapekKnowledge);
            mapekPlan = new MyMapekPlan(mock, false);
        }

        private static void GenerateFromPython(string fromPython, string outPath, string executingAssemblyPath) {
            var modelDirPath = Path.Combine(executingAssemblyPath, "models-and-rules");
            Assert.True(File.Exists(Path.Combine(modelDirPath, fromPython)));
            Assert.True(File.Exists(Path.Combine(modelDirPath, "RDTBindings.py")));
            if (runInference || !File.Exists(outPath) || File.GetLastWriteTime(Path.Combine(modelDirPath, fromPython)) > File.GetLastWriteTime(outPath) || File.GetLastWriteTime(Path.Combine(modelDirPath, "RDTBindings.py")) > File.GetLastWriteTime(outPath)) {
                runInference = true;
                Trace.WriteLine("Regenerating model...");
                var processInfo = new ProcessStartInfo {
                    FileName = "python3",
                    Arguments = Path.Combine(modelDirPath, fromPython),
                    RedirectStandardOutput = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
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

            public class AMQSensor(string sensorName, string procedureName, Func<IncubatorFields, double> f) : ISensor
            {
                private bool _onceOnly = true;

                public string SensorName { get; private init; } = sensorName;

                public string ProcedureName { get; private init; } = procedureName;

                public object ObservePropertyValue(params object[] inputProperties)
                {
                    Assert.True(_onceOnly, "Really just expecting it to be called once here.");
                    IncubatorFields? myData = null;
                    Monitor.Enter(i);
                    myData = i.Data;
                    Monitor.Exit(i);
                    Debug.Assert(myData != null, "No data received from Incubator AMQP.");
                    _onceOnly = false;
                    return f(myData);
                }
            }

            public ISensor GetSensorDeviceImplementation(string sensorName, string procedureName) {
                if (sensorName == "http://www.semanticweb.org/vs/ontologies/2025/12/incubator#TempSensor") {
                    if (procedureName == "http://www.semanticweb.org/vs/ontologies/2025/12/incubator#TempProcedure") {
                        return AMQTempSensor;
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