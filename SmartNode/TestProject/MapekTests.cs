using Fitness;
using Logic.CaseRepository;
using Logic.FactoryInterface;
using Logic.Mapek;
using Logic.Models.DatabaseModels;
using Logic.Models.MapekModels;
using MongoDB.Driver;
using System.Diagnostics;
using System.Reflection;
using TestProject.Mocks.ServiceMocks;

namespace TestProject
{
    public class MapekTests
    {
        [Theory]
        // [InlineData("M370-instance.ttl", "M370-inferred.ttl",1,4,115.05600000000001, false, false, false)]
        [InlineData(2, 1800, 40, 106.488, true, false, false)]
        [InlineData(4, 900, 5536, 114.696, false, false, false)]
        [InlineData(4, 900, 5536, 114.696, false, true, false)]
        [InlineData(4, 900, 5536, 114.696, false, true, true)]
        [InlineData(5, 720, 5536, 114.696, false, true, false)]
        //[InlineData(4, 5536, 114.696, true, true, false)]
        public async Task TestM370(int rounds, int duration, int count, double minCost, bool useCase, bool nullLogger, bool dontMinimize)
        {
            // Arrange
            IRDTServiceProvider serviceProvider = nullLogger ? new NullServiceProviderMock() : new ServiceProviderMock();

            var rootDirectory = Directory.GetParent(Assembly.GetExecutingAssembly().Location)!.Parent!.Parent!.Parent!.Parent!.Parent!.FullName;
            var filepathArguments = new FilepathArguments
            {
                DataDirectory = "",
                FmuDirectory = Path.GetFullPath(Path.Combine(rootDirectory, "SmartNode", "Implementations", "FMUs")),
                InferenceEngineFilepath = Path.GetFullPath(Path.Combine(rootDirectory, "models-and-rules", "ruleless-digital-twins-inference-engine.jar")),
                InferenceRulesFilepath = Path.GetFullPath(Path.Combine(rootDirectory, "models-and-rules", "inference-rules.rules")),
                InferredModelFilepath = Path.GetFullPath(Path.Combine(rootDirectory, "models-and-rules", "M370-simulation-inferred.ttl")),
                InstanceModelFilepath = Path.GetFullPath(Path.Combine(rootDirectory, "models-and-rules", "M370.ttl")),
                OntologyFilepath = Path.GetFullPath(Path.Combine(rootDirectory, "ontology", "ruleless-digital-twins.ttl"))
            };
            serviceProvider.Add(filepathArguments);

            var coordinatorSettings = new CoordinatorSettings
            {
                Environment = "roomM370",
                LookAheadMapekCycles = rounds,
                MaximumMapekRounds = 1,
                PropertyValueFuzziness = 0.25,
                SaveMapekCycleData = false,
                CycleDurationSeconds = duration,
                SleepyTimeMilliseconds = 0,
                StartInReactiveMode = false,
                UseCaseBasedFunctionality = useCase,
                UseDecisionLagMitigation = false, // XXX not supported for multiple FMUs yet.
                UseEuclid = false
            };
            serviceProvider.Add(coordinatorSettings);

            IFactory factory = new SmartNode.Factory(coordinatorSettings.Environment);
            serviceProvider.Add(factory);

            IMapekKnowledge knowledge = new MapekKnowledge(serviceProvider);
            serviceProvider.Add(knowledge);

            IMapekMonitor monitor = new MapekMonitor(serviceProvider);
            serviceProvider.Add(monitor);

            if (useCase) {
                var db = new DatabaseSettings() {ConnectionString = "mongodb://172.22.0.2:27017", DatabaseName = "testdb", CollectionName = "cases"};
                serviceProvider.Add(db);
                IMongoClient imc = new MongoClient(db.ConnectionString);
                serviceProvider.Add(imc);
                serviceProvider.Add(new CaseRepository(serviceProvider));
            }

            // Test property we want to accumulate:
            var f_energy = new FProp("http://www.semanticweb.org/ivans/ontologies/2025/instance-model-1#EnergyConsumption");
            var f_temp = new FProp("http://www.semanticweb.org/ivans/ontologies/2025/instance-model-1#price");
            var f_prod = new FBinOpArith(f_energy, f_temp, (x, y) => x * y, name: "http://www.semanticweb.org/ivans/ontologies/2025/instance-model-1#EnergyTimesPrice");
            var f_prod_acc = new FAcc<double>(f_prod, name: "http://www.semanticweb.org/ivans/ontologies/2025/instance-model-1#AccumulatedEnergyTimesPrice");

            MapekPlan plan = new MMK(serviceProvider, new FOp[] { f_prod_acc });
            // Adjust for hard-coded accumulation:
            plan._minMaxOverrides = false;
            serviceProvider.Add(plan);

            Assert.Equal(2, plan.GetHostPlatformFmuModel(filepathArguments.FmuDirectory).Count());

            // Act
            try
            {
                if (dontMinimize) {
                    var deleteQ = ((MapekKnowledge)knowledge).GetParameterizedStringQuery("DELETE DATA { <http://www.semanticweb.org/ivans/ontologies/2025/instance-model-1#RoomM370> meta:minimizes <http://www.semanticweb.org/ivans/ontologies/2025/instance-model-1#AccumulatedEnergyTimesPrice> }");
                    ((MapekKnowledge)knowledge).UpdateModel(deleteQ);
                }
                var cache = await monitor.Monitor(0);

                Stopwatch sw = Stopwatch.StartNew();
                var simulationPathAndTree = await plan.Plan(cache, 0);
                sw.Stop();
                Debug.WriteLine($"Planning took {sw.Elapsed.TotalSeconds} seconds total.");

            using (StreamWriter f_out = File.AppendText("plan_times.csv")) {
                    // Check that all best paths are as they should be:
                    IEnumerable<SimulationPath> paths = plan.GetOptimalSimulationPath(cache, simulationPathAndTree.Item1.SimulationPaths);
                    // Print starting values:
                    var sroot = simulationPathAndTree.Item1.NodeItem.PropertyCache; {
                        var temp = sroot.Properties["http://www.semanticweb.org/ivans/ontologies/2025/instance-model-1#RoomTemperature"].Value;                        
                        f_out.WriteLine($"0,{temp},0,0,0,0,0,0,\"\"");
                    }

                // Assume worst case if we're not minimizing
                double minOrMax = dontMinimize  ? (double)paths.Max(s => s.Simulations.Last().PropertyCache.Properties["http://www.semanticweb.org/ivans/ontologies/2025/instance-model-1#AccumulatedEnergyTimesPrice"].Value)
                                                : (double)paths.Min(s => s.Simulations.Last().PropertyCache.Properties["http://www.semanticweb.org/ivans/ontologies/2025/instance-model-1#AccumulatedEnergyTimesPrice"].Value);
                var sims = paths.Where(s => (double)s.Simulations.Last().PropertyCache.Properties["http://www.semanticweb.org/ivans/ontologies/2025/instance-model-1#AccumulatedEnergyTimesPrice"].Value == minOrMax);

                // Remember to print initial values at some point...
                Debug.WriteLine(simulationPathAndTree.Item1.NodeItem.PropertyCache.Properties["http://www.semanticweb.org/ivans/ontologies/2025/instance-model-1#RoomTemperature"].Value);
                foreach (Simulation s in sims.First().Simulations) {
                  var actions = string.Join(",", s.Actions.OrderBy(a => a.Name).Select(a => a.Name.Split("#")[1].Split("_")[1]));
                  var temp = s.PropertyCache.Properties["http://www.semanticweb.org/ivans/ontologies/2025/instance-model-1#RoomTemperature"].Value;
                  var consumption = s.PropertyCache.Properties[f_energy.Prop.Name].Value;
                  var price = s.PropertyCache.Properties[f_temp.Prop.Name].Value;
                  var accPrice = s.PropertyCache.Properties["http://www.semanticweb.org/ivans/ontologies/2025/instance-model-1#AccumulatedEnergyTimesPrice"].Value;
                  f_out.Write($"{(s.Index+1) * duration},{temp},{accPrice},{consumption},{price},{actions},");
                  f_out.WriteLine($"\"{ThisAssembly.Git.Commit}{(ThisAssembly.Git.IsDirty ? "-DIRTY" : "")}: {rounds},{count},{minCost},case:{useCase},dMin:{dontMinimize},{sw.Elapsed.TotalSeconds}s\"");
                }
            }

            // Important stuff stops here.
            // Safety net below.
                var path = simulationPathAndTree.Item2.Simulations;
                var last = path.Last().PropertyCache.Properties["http://www.semanticweb.org/ivans/ontologies/2025/instance-model-1#AccumulatedEnergyTimesPrice"].Value;

                // We compute the same value in different ways just to be sure:
                var f_prod2 = new FBinOpArith(f_energy, f_temp, (x, y) => x * y, name: "http://www.semanticweb.org/ivans/ontologies/2025/instance-model-1#EnergyTimesPriceB");
                var f_prod_acc2 = new FAcc<double>(f_prod2, name: "http://www.semanticweb.org/ivans/ontologies/2025/instance-model-1#AccumulatedEnergyTimesPriceB");

                // Sanity check root-node in case we messed it up in-place:
                {
                    var found = simulationPathAndTree.Item1.NodeItem.PropertyCache.Properties.TryGetValue("http://www.semanticweb.org/ivans/ontologies/2025/instance-model-1#AccumulatedEnergyTimesPrice", out var vOut);
                    Assert.True(!found || (string)vOut!.Value == "0");
                }

                Fitness.Fitness fitness = new(simulationPathAndTree.Item1.NodeItem)
                {
                    FOps = new FOp[] { f_prod_acc2 }
                };

                Assert.Equal(coordinatorSettings.LookAheadMapekCycles, path.Count());
                var result = path.Aggregate(fitness.MkState(), fitness.Process);
                Debug.WriteLine(string.Join(",", fitness.FOps.Select(fop => fop.Prop.Name + "=" + result.Get(fop.Prop))));

                var r2 = path.Aggregate(0.0, (acc, s) => acc + ((double)s.PropertyCache.Properties["http://www.semanticweb.org/ivans/ontologies/2025/instance-model-1#price"].Value) * (double)s.PropertyCache.Properties["http://www.semanticweb.org/ivans/ontologies/2025/instance-model-1#EnergyConsumption"].Value);
                Assert.Equal(result.Get(f_prod_acc2.Prop), r2);
                Assert.Equal(result.Get(f_prod_acc2.Prop), last);
                
                //Assert.Equal(count, paths.Count());
                // XXX We're not doing this atm. Assert.Equal(minCost, least);
                //Assert.Single(leasts); // Doesn't have to be true, but is.
            }
            catch (Exception exception)
            {
                Debug.WriteLine(exception.Message);
                throw;
            }
        }

        [Theory]
        [InlineData(2, 1800, 106.488, true, false, false)]
        [InlineData(4, 900, 114.696, false, false, false)]
        [InlineData(4, 900, 114.696, false, true, false)]
        [InlineData(4, 900, 114.696, false, true, true)]
        [InlineData(5, 720, 114.696, false, true, false)]
        //[InlineData(4, 5536, 114.696, true, true, false)]
        public async Task TestM370Ivan(int rounds, int duration, double minCost, bool useCase, bool nullLogger, bool dontMinimize)
        {
            // Arrange
            IRDTServiceProvider serviceProvider = nullLogger ? new NullServiceProviderMock() : new ServiceProviderMock();

            var rootDirectory = Directory.GetParent(Assembly.GetExecutingAssembly().Location)!.Parent!.Parent!.Parent!.Parent!.Parent!.FullName;
            var filepathArguments = new FilepathArguments
            {
                DataDirectory = "",
                FmuDirectory = Path.GetFullPath(Path.Combine(rootDirectory, "SmartNode", "Implementations", "FMUs")),
                InferenceEngineFilepath = Path.GetFullPath(Path.Combine(rootDirectory, "models-and-rules", "ruleless-digital-twins-inference-engine.jar")),
                InferenceRulesFilepath = Path.GetFullPath(Path.Combine(rootDirectory, "models-and-rules", "inference-rules.rules")),
                InferredModelFilepath = Path.GetFullPath(Path.Combine(rootDirectory, "models-and-rules", "M370-inferred.ttl")),
                InstanceModelFilepath = Path.GetFullPath(Path.Combine(rootDirectory, "models-and-rules", "M370-instance.ttl")),
                OntologyFilepath = Path.GetFullPath(Path.Combine(rootDirectory, "ontology", "ruleless-digital-twins.ttl"))
            };
            serviceProvider.Add(filepathArguments);

            var coordinatorSettings = new CoordinatorSettings
            {
                Environment = "roomM370",
                LookAheadMapekCycles = rounds,
                MaximumMapekRounds = 1,
                PropertyValueFuzziness = 0.25,
                SaveMapekCycleData = false,
                CycleDurationSeconds = duration,
                SleepyTimeMilliseconds = 0,
                StartInReactiveMode = false,
                UseCaseBasedFunctionality = useCase,
                UseDecisionLagMitigation = false, // XXX not supported for multiple FMUs yet.
                UseEuclid = false
            };
            serviceProvider.Add(coordinatorSettings);

            IFactory factory = new SmartNode.Factory(coordinatorSettings.Environment);
            serviceProvider.Add(factory);

            IMapekKnowledge knowledge = new MapekKnowledge(serviceProvider);
            serviceProvider.Add(knowledge);

            IMapekMonitor monitor = new MapekMonitor(serviceProvider);
            serviceProvider.Add(monitor);

            if (useCase) {
                var db = new DatabaseSettings() {ConnectionString = "mongodb://172.22.0.2:27017", DatabaseName = "testdb", CollectionName = "cases"};
                serviceProvider.Add(db);
                IMongoClient imc = new MongoClient(db.ConnectionString);
                serviceProvider.Add(imc);
                serviceProvider.Add(new CaseRepository(serviceProvider));
            }

            IMapekPlan plan = new MMK(serviceProvider, new FOp[] { });
            serviceProvider.Add(plan);

            Assert.Equal(2, ((MapekPlan)plan).GetHostPlatformFmuModel(filepathArguments.FmuDirectory).Count());

            // Act
            try
            {
                if (dontMinimize) {
                    var deleteQ = ((MapekKnowledge)knowledge).GetParameterizedStringQuery("DELETE DATA { <http://www.semanticweb.org/ivans/ontologies/2025/instance-model-1#RoomM370> meta:minimizes <http://www.semanticweb.org/ivans/ontologies/2025/instance-model-1#PricePerEnergy> }");
                    ((MapekKnowledge)knowledge).UpdateModel(deleteQ);
                }
                var cache = await monitor.Monitor(0);

                Stopwatch sw = Stopwatch.StartNew();
                var simulationPathAndTree = await plan.Plan(cache, 0);
                sw.Stop();
                Debug.WriteLine($"Planning took {sw.Elapsed.TotalSeconds} seconds total.");

                IEnumerable<SimulationPath> paths = plan.GetOptimalSimulationPath(cache, simulationPathAndTree.Item1.SimulationPaths);
            using (StreamWriter f_out = File.AppendText("plan_times_ivan.csv")) {
                    // Check that all best paths are as they should be:
                    // Print starting values:
                    var sroot = simulationPathAndTree.Item1.NodeItem.PropertyCache; {
                        var temp = sroot.Properties["http://www.semanticweb.org/ivans/ontologies/2025/instance-model-1#RoomTemperature"].Value;                        
                        f_out.WriteLine($"0,{temp},0,0,0,0,\"\"");
                    }

                // Assume worst case if we're not minimizing
                double minOrMax = dontMinimize  ? (double)paths.Max(s => s.Simulations.Last().PropertyCache.Properties["http://www.semanticweb.org/ivans/ontologies/2025/instance-model-1#PricePerEnergy"].Value)
                                                : (double)paths.Min(s => s.Simulations.Last().PropertyCache.Properties["http://www.semanticweb.org/ivans/ontologies/2025/instance-model-1#PricePerEnergy"].Value);
                var sims = paths.Where(s => (double)s.Simulations.Last().PropertyCache.Properties["http://www.semanticweb.org/ivans/ontologies/2025/instance-model-1#PricePerEnergy"].Value == minOrMax);

                // Write initial values:
                foreach (Simulation s in sims.First().Simulations) {
                  var actions = string.Join(",", s.Actions.OrderBy(a => a.Name).Select(a => a.Name.Split("#")[1].Split("_")[1]));
                  var temp = s.PropertyCache.Properties["http://www.semanticweb.org/ivans/ontologies/2025/instance-model-1#RoomTemperature"].Value;
                  var accPrice = s.PropertyCache.Properties["http://www.semanticweb.org/ivans/ontologies/2025/instance-model-1#PricePerEnergy"].Value;
                  f_out.Write($"{(s.Index+1) * duration},{temp},{-0.0},{actions},");
                  f_out.WriteLine($"\"{ThisAssembly.Git.Commit}{(ThisAssembly.Git.IsDirty ? "-DIRTY" : "")}: {rounds},{minCost},case:{useCase},dMin:{dontMinimize},{sw.Elapsed.TotalSeconds}s\"");
                }
            }

            // Important stuff stops here.
            // Safety net below.
            {
                var path = simulationPathAndTree.Item2.Simulations;
                var last = path.Last().PropertyCache.Properties["http://www.semanticweb.org/ivans/ontologies/2025/instance-model-1#PricePerEnergy"].Value;
                
                Assert.Single(paths);
                // Assert some more.
            };
            }
            catch (Exception exception)
            {
                Debug.WriteLine(exception.Message);
                throw;
            }
        }
    }

    internal class MMK : MapekPlan {
        private readonly IEnumerable<FOp> _fOps;

        public MMK(IServiceProvider serviceProvider, IEnumerable<FOp> fOps) : base(serviceProvider) {
            _fOps = fOps;
        }
        public override IEnumerable<FOp> GetFitnessOps() {
            return _fOps;
        }
    }
}