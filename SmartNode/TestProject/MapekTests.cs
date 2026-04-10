using Fitness;
using Logic.CaseRepository;
using Logic.FactoryInterface;
using Logic.Mapek;
using Logic.Models.DatabaseModels;
using Logic.Models.MapekModels;
using Logic.Models.OntologicalModels;
using MongoDB.Driver;
using System.Diagnostics;
using System.Reflection;
using TestProject.Mocks.ServiceMocks;

namespace TestProject
{
    public class MapekTests
    {
        [Theory]
        [InlineData("instance-model-1.ttl", "inferred-model-1.ttl",2,40,0, false, false)]
        [InlineData("M370-instance.ttl", "M370-inferred.ttl",1,4,115.05600000000001, false, false)]
        [InlineData("M370-instance.ttl", "M370-inferred.ttl", 2, 40, 106.488, true, false)]
        [InlineData("M370-instance.ttl", "M370-inferred.ttl", 4, 5536, 114.696, false, true)]
        [InlineData("M370-instance.ttl", "M370-inferred.ttl", 4, 5536, 114.696, true, true)]
        [InlineData("M370-instance.ttl", "M370-inferred.ttl", 8, 0, 0, false, true)]
        [InlineData("M370-instance.ttl", "M370-inferred.ttl", 8, 0, 0, true, true)]
        public async Task TestMapeK(String instance, String inferred, int rounds, int count, double minCost, bool useCase, bool nullLogger)
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
                InferredModelFilepath = Path.GetFullPath(Path.Combine(rootDirectory, "models-and-rules", inferred)),
                InstanceModelFilepath = Path.GetFullPath(Path.Combine(rootDirectory, "models-and-rules", instance)),
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
                CycleDurationSeconds = 3600,
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

            IMapekPlan plan = new MMK(serviceProvider, new FOp[] { f_prod_acc });
            serviceProvider.Add(plan);

            // Act
            try
            {
                var cache = await monitor.Monitor();
                { // Tweak cache:
                    cache.PropertyCache.Properties.Add("http://www.semanticweb.org/ivans/ontologies/2025/instance-model-1#price",
                        new Property { Name = "http://www.semanticweb.org/ivans/ontologies/2025/instance-model-1#price", Value = 0.0, OwlType = "http://www.w3.org/2001/XMLSchema#double" });
                }

                Stopwatch sw = Stopwatch.StartNew();
                var simulationPathAndTree = await plan.Plan(cache);
                sw.Stop();
                Debug.WriteLine($"Planning took {sw.Elapsed.TotalSeconds} seconds total.");
                using (StreamWriter f_out = File.AppendText("plan_times.txt")) { f_out.WriteLine($"{ThisAssembly.Git.Commit}{(ThisAssembly.Git.IsDirty ? "-DIRTY" : "")}: {instance},{rounds},{count},{minCost},{useCase},{sw.Elapsed.TotalSeconds}"); }

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

                // Check that all best paths are as they should be:
                var paths = plan.GetOptimalSimulationPath(cache, simulationPathAndTree.Item1.SimulationPaths);
                double least = (double)paths.Min(s => s.Simulations.Last().PropertyCache.Properties["http://www.semanticweb.org/ivans/ontologies/2025/instance-model-1#AccumulatedEnergyTimesPrice"].Value);
                var leasts = paths.Where(s => (double)s.Simulations.Last().PropertyCache.Properties["http://www.semanticweb.org/ivans/ontologies/2025/instance-model-1#AccumulatedEnergyTimesPrice"].Value == least);
                Debug.WriteLine(string.Join(",\n", leasts.First().Simulations.Select(s => string.Join(" + ", s.Actions.Select(a => a.Name)))));
                
                Assert.Equal(count, paths.Count());
                Assert.Equal(minCost, least);
                Assert.Single(leasts); // Doesn't have to be true, but is.
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