using Implementations.ValueHandlers;
using Logic.TTComponentInterfaces;
using Logic.FactoryInterface;
using Logic.Mapek;
using Logic.Models.MapekModels;
using Logic.Models.OntologicalModels;
using Logic.ValueHandlerInterfaces;
using System.Diagnostics;
using System.Reflection;
using TestProject.Mocks.ServiceMocks;
using Implementations.SimulatedTwinningTargets;

namespace TestProject {
    public class IncubatorTests : IDisposable {
        static bool runInference = false; // `false` can be overriden by logic below.
        // IP is coming from "docket network create // inspect" -> rabbitmq-ip or variations thereof:
        static readonly IncubatorAdapter i = new("172.21.0.2", TestContext.Current.CancellationToken);
        static readonly Factory.AMQSensor AMQTempSensor = new("http://www.semanticweb.org/vs/ontologies/2025/12/incubator#TempSensor", "http://www.semanticweb.org/vs/ontologies/2025/12/incubator#TempProcedure", ((d) => d.average_temperature));
        static bool crashed = true;
        private static MyMapekPlan _mapekPlan;

        private class MyMapekPlan : MapekPlan {

            public MyMapekPlan(IServiceProvider serviceProvider) : base(serviceProvider) { }

            protected override void InferActionCombinations() {
                // Call Java explicitly?
                if (IncubatorTests.runInference) {
                    base.InferActionCombinations();
                }
            }
        }

        [Theory]
        [InlineData(21.0, true, "Incubator.py", "incubator.ttl", "incubator-out.ttl", 4)]
        // Higher temps may fail since I currently don't handle the upper bound correctly:
        [InlineData(37.0, false, "Incubator.py", "incubator.ttl", "incubator-out.ttl", 4)]
        // This one used to glitch with an INF-crash in the FMU, but now passes?!
        [InlineData(53.2359909270973, false, "Incubator.py", "incubator.ttl", "incubator-out.ttl", 4)]
        public void SimulateFMUOnly(double initial_T_value, bool all_actuators, string fromPython, string model, string inferred, int lookAheadCycles) {
            SetupFiles(fromPython, model, inferred, out ServiceProviderMock mock, out FilepathArguments filepathArguments, out MapekKnowledge mapekKnowledge, out MyMapekPlan mapekPlan);

            // TODO: Prototype populate cache from FMU.
            // If we're going to do this, we have to check that we correctly override with values from model.
            var fmu = Femyou.Model.Load(Path.Combine(filepathArguments.FmuDirectory, "au_incubator.fmu")); // TODO: grab from model
            var (SvType, SvValue) = fmu.Variables["G_box"]!.StartValue;
            Assert.Equal("Real", SvType);
            double gbox = double.Parse(SvValue);
            fmu.Dispose(); // Don't forget this or you'll get segfaults when loading the FMU "again" later.
            // END Prototype

            var propertyCacheMock = new PropertyCache {
                ConfigurableParameters = new Dictionary<string, ConfigurableParameter>(),
                // TODO: Ideally we wouldn't need those, and either start with `undefined` or use the FMU's values.

                Properties = new Dictionary<string, Property> {
                    {
                        "http://www.semanticweb.org/vs/ontologies/2025/12/incubator#in_room_temperature",
                        new Property {
                            Name = "http://www.semanticweb.org/vs/ontologies/2025/12/incubator#in_room_temperature",
                            OwlType = "http://www.w3.org/2001/XMLSchema#double",
                            Value = 21.0
                        }
                    },
                   {
                        "http://www.semanticweb.org/vs/ontologies/2025/12/incubator#T",
                        new Property {
                            Name = "http://www.semanticweb.org/vs/ontologies/2025/12/incubator#T",
                            OwlType = "http://www.w3.org/2001/XMLSchema#double",
                            Value = initial_T_value
                        }
                    },
                   {
                        "http://www.semanticweb.org/vs/ontologies/2025/12/incubator#G_box",
                        new Property {
                            Name = "http://www.semanticweb.org/vs/ontologies/2025/12/incubator#G_box",
                            OwlType = "http://www.w3.org/2001/XMLSchema#double",
                            Value = gbox
                        }
                    }
                }
            };

            mapekKnowledge.Validate(propertyCacheMock);

            // TODO: Assert that there's at least one actuator that's not a parameter.
            var (simulationTree, simulationPath) = mapekPlan.Plan(new Cache() { PropertyCache = propertyCacheMock, OptimalConditions = [], SoftSensorTreeNodes = [] }).Result;
            crashed = false;

            // Only valid AFTER focing evaluation through simulation:
            Assert.Equal(Math.Pow(2, lookAheadCycles), simulationTree.SimulationPaths.Count());
            Assert.Equal(30, simulationTree.ChildrenCount);

            Trace.WriteLine("Checking Volker's `best` solution:");
                        // We move this up here in the test since it may spam the log:
            IEnumerable<OptimalCondition> optimalConditions = mapekKnowledge.GetAllOptimalConditions(propertyCacheMock);
            Assert.NotEmpty(optimalConditions);

            // Note that the optimal conditions are coming from the INITIAL model, but will be evaluated in the 
            //  LAST state of the simulations!
            var vs = mapekPlan.GetOptimalSimulationPathsEuclidian(simulationTree.SimulationPaths, optimalConditions);
            Trace.WriteLine($"{vs.Count()} solutions: {string.Join(",",vs.Select(pd => pd.Item2))}");
            // We know that for OUR tests, we either pull out all stops or keep all off.
            Assert.True(!all_actuators || vs.First().Item2 < vs.ElementAt(1).Item2);
            var path = vs.First().Item1;

            // var path = optimalSimulationPath;
            foreach (var s in path.Simulations) {
                Trace.WriteLine(string.Join(";", s.Actions.Select(a => a.Name)));
                Trace.WriteLine("Params: " + string.Join(";", s.InitializationActions.Select(a => a.Name).ToList()));
                Trace.WriteLine("Inputs: " + string.Join(";", s.Actions.Select(a => a.Name).ToList()));
            }

            // Cold room, assert that the optimal path is heading in the right direction:
            Assert.Equal(4, path.Simulations.Count());
            foreach (var s in path.Simulations) {
                Assert.True(s.Actions.All(a => (all_actuators ? "1" : "0") == ((ActuationAction)a).NewStateValue.ToString()));
            }
            crashed = false;
        }

        [Theory]
        [InlineData("Incubator.py", "incubator.ttl", "incubator-out.ttl", 4)]
        public void SimulateFromAMQ(string fromPython, string model, string inferred, int lookAheadCycles) {
            SetupFiles(fromPython, model, inferred, out ServiceProviderMock mock, out FilepathArguments filepathArguments, out MapekKnowledge mapekKnowledge, out MyMapekPlan mapekPlan);
            IMapekExecute mpe;
            mock.Add(mpe = new MapekExecute(mock));

            // Run things synchronously until we figure out how to retrive exceptions that xUnit swallows.
#pragma warning disable xUnit1051 // Probably okay for now since we gave the Adapter the token already.
            i.Connect().Wait();
            var consumerTag = i.Setup().Result;

            var monitor = new MapekMonitor(mock);
            Assert.True(AMQTempSensor._onceOnly);
            var cache = monitor.Monitor().Result;

            var (simulationTree, optimalSimulationPath) = mapekPlan.Plan(cache).Result;
            crashed = false;
            Assert.False(AMQTempSensor._onceOnly); // Must've been used.

            // Only valid AFTER focing evaluation through simulation:
            Assert.Equal(Math.Pow(2, lookAheadCycles), simulationTree.SimulationPaths.Count());
            Assert.Equal(30, simulationTree.ChildrenCount);

            Trace.WriteLine("Checking Volker's `best` solution:");
            IEnumerable<OptimalCondition> optimalConditions = mapekKnowledge.GetAllOptimalConditions(cache.PropertyCache);
            Assert.NotEmpty(optimalConditions);
            var vs = mapekPlan.GetOptimalSimulationPathsEuclidian(simulationTree.SimulationPaths, optimalConditions);
            Trace.WriteLine($"{vs.Count()} solutions: {string.Join(",",vs.Select(pd => pd.Item2))}");
            var path = vs.First().Item1;

            foreach (var s in path.Simulations)
            {
                Trace.WriteLine(string.Join(";", s.Actions.Select(a => a.Name)));
                Trace.WriteLine("Params: " + string.Join(";", s.InitializationActions.Select(a => a.Name).ToList()));
                Trace.WriteLine("Inputs: " + string.Join(";", s.Actions.Select(a => a.Name).ToList()));
            }
            mpe.Execute(path.Simulations.First()).Wait();
            crashed = false;
#pragma warning restore xUnit1051
        }

        private static void SetupFiles(string fromPython, string model, string inferred, out ServiceProviderMock mock, out FilepathArguments filepathArguments, out MapekKnowledge mapekKnowledge, out MyMapekPlan mapekPlan) {
            crashed = true;
            if (Directory.Exists("/tmp/Femyou")) {
                Directory.Delete("/tmp/Femyou", true);
            }
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
                DateTime x, y;
                runInference = !((x = File.GetLastWriteTime(inferredFilePath)) > (y = File.GetLastWriteTime(modelFilePath)));
                if (runInference) {
                    Trace.WriteLine($"Will regenerate inferred model because {inferredFilePath} ({x}) is older than {modelFilePath} ({y})");
                }
            }

            mock = new ServiceProviderMock();
            mock.Add<IFactory>(new Factory());
            filepathArguments = new FilepathArguments {
                InstanceModelFilepath = modelFilePath,
                InferredModelFilepath = inferredFilePath,
                InferenceEngineFilepath = Path.Combine(rootDirectory, "models-and-rules", "ruleless-digital-twins-inference-engine.jar"),
                InferenceRulesFilepath = Path.Combine(rootDirectory, "models-and-rules", "inference-rules.rules"),
                OntologyFilepath = Path.Combine(rootDirectory, "ontology", "ruleless-digital-twins.ttl"),
                DataDirectory = Path.Combine(rootDirectory, "state-data"),
                FmuDirectory = Path.Combine(rootDirectory, "SmartNode", "Implementations", "FMUs")
            };
            mock.Add(filepathArguments);
            mock.Add(new CoordinatorSettings {
                LookAheadMapekCycles = 4,
                MaximumMapekRounds = 4,
                StartInReactiveMode = false,
                SimulationDurationSeconds = 10,
                Environment = "incubator"
            });
            mapekKnowledge = new MapekKnowledge(mock);
            mock.Add<IMapekKnowledge>(mapekKnowledge);
            mapekPlan = new MyMapekPlan(mock);
            _mapekPlan = mapekPlan; // save for crash-handling.
        }

        private static void GenerateFromPython(string fromPython, string outPath, string executingAssemblyPath) {
            var modelDirPath = Path.Combine(executingAssemblyPath, "models-and-rules");
            Assert.True(File.Exists(Path.Combine(modelDirPath, fromPython)));
            Assert.True(File.Exists(Path.Combine(modelDirPath, "RDTBindings.py")));
            if (runInference || !File.Exists(outPath) || File.GetLastWriteTime(Path.Combine(modelDirPath, fromPython)) > File.GetLastWriteTime(outPath) || File.GetLastWriteTime(Path.Combine(modelDirPath, "RDTBindings.py")) > File.GetLastWriteTime(outPath)) {
                runInference = true;
                Trace.WriteLine("Regenerating model...");
                var processInfo = new ProcessStartInfo {
                    FileName = "python3", // TODO: Pick up from environment, might be just "python" for others.
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

        public void Dispose() {
            // Marker in output because sometimes FMU-crashes confuse the testing framework:
            if (crashed) {
                Trace.WriteLine("We crashed 🥺");
            } else {
                Trace.WriteLine("We didn't crash, yay!");
            }
            _mapekPlan.Dispose();
            Assert.False(crashed);
        }

        internal class Factory : IFactory {
            private readonly Dictionary<string, IValueHandler> _valueHandlers = new() {
                { "http://www.w3.org/2001/XMLSchema#double", new DoubleValueHandler() },
                { "http://www.w3.org/2001/XMLSchema#string", new StringValueHandler() },
                { "http://www.w3.org/2001/XMLSchema#int", new IntValueHandler() }
            };
            public IActuator GetActuatorImplementation(string actuatorName) {
                if ("http://www.semanticweb.org/vs/ontologies/2025/12/incubator#HeaterActuator".Equals(actuatorName)) {
                    return new AMQHeater();
                } else {
                    throw new NotImplementedException(actuatorName);
                }
            }

            public IConfigurableParameter GetConfigurableParameterImplementation(string configurableParameterName) {
                throw new NotImplementedException();
            }

            internal class AMQHeater() : IActuator
            {
                public string ActuatorName => "Incubator Heater";

                public async Task Actuate(object state) {
                    var _actuatorState = int.Parse((string)state);
                    if (_actuatorState == 0) {
                        Task t = Task.Run(async () => await i.SetHeater(false));
                    } else if (_actuatorState == 1) {
                        Task t = Task.Run(async () => await i.SetHeater(true));
                        t.Wait();
                    } else {
                        Debug.Fail($"Unexpected value {_actuatorState}!");
                    }
                }
            }

            public class AMQSensor(string sensorName, string procedureName, Func<IncubatorFields, double> f) : ISensor
            {
                public bool _onceOnly = true;

                public string SensorName { get; private init; } = sensorName;

                public string ProcedureName { get; private init; } = procedureName;

                public async Task<object> ObservePropertyValue(params object[] inputProperties) {
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

            public ISensor GetSensorImplementation(string sensorName, string procedureName) {
                if (sensorName == "http://www.semanticweb.org/vs/ontologies/2025/12/incubator#TempSensor") {
                    if (procedureName == "http://www.semanticweb.org/vs/ontologies/2025/12/incubator#TempProcedure") {
                        return AMQTempSensor;
                    }
                }
                throw new NotImplementedException($"{sensorName}/{procedureName}");
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