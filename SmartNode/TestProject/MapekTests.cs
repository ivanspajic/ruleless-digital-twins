using Fitness;
using Logic.FactoryInterface;
using Logic.Mapek;
using Logic.Models.MapekModels;
using Logic.Models.OntologicalModels;
using System.Diagnostics;
using System.Numerics;
using System.Reflection;
using TestProject.Mocks.ServiceMocks;
using Xunit.Internal;

namespace TestProject
{
    public class MapekTests
    {
        [Theory]
        [InlineData("instance-model-1.ttl", "inferred-model-1.ttl",2,40)]
        [InlineData("M370-instance.ttl", "M370-inferred.ttl",1,4)]
        [InlineData("M370-instance.ttl", "M370-inferred.ttl",2,40)]
        public async Task TestMapeK(String instance, String inferred, int rounds, int count)
        {
            // Arrange
            var serviceProvider = new ServiceProviderMock();

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
                UseCaseBasedFunctionality = false,
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

            // Test property we want to accumulate:
            var f_energy = new FProp("http://www.semanticweb.org/ivans/ontologies/2025/instance-model-1#EnergyConsumption");
            var f_temp = new FProp("http://www.semanticweb.org/ivans/ontologies/2025/instance-model-1#price");
            var f_prod = new FBinOpArith(f_energy, f_temp, (x, y) => x * y, name: "http://www.semanticweb.org/ivans/ontologies/2025/instance-model-1#EnergyTimesPrice");
            var f_prod_acc = new FAcc<double>(f_prod, name: "http://www.semanticweb.org/ivans/ontologies/2025/instance-model-1#AccumulatedEnergyTimesPrice");

            // IMapekPlan plan = new MapekPlan(serviceProvider);
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
                var simulationPathAndTree = await plan.Plan(cache);
                var path = simulationPathAndTree.Item2.Simulations;
                var last = path.Last().PropertyCache.Properties["http://www.semanticweb.org/ivans/ontologies/2025/instance-model-1#AccumulatedEnergyTimesPrice"].Value;
                Debug.WriteLine("Overall NOK: " + last);

                // We compute the same value in different ways just to be sure:
                var f_prod2 = new FBinOpArith(f_energy, f_temp, (x, y) => x * y, name: "http://www.semanticweb.org/ivans/ontologies/2025/instance-model-1#EnergyTimesPriceB");
                var f_prod_acc2 = new FAcc<double>(f_prod2, name: "http://www.semanticweb.org/ivans/ontologies/2025/instance-model-1#AccumulatedEnergyTimesPriceB");

                // Sanity check root-node in case we messed it up in-place:
                {                    
                    var found = simulationPathAndTree.Item1.NodeItem.PropertyCache.Properties.TryGetValue("http://www.semanticweb.org/ivans/ontologies/2025/instance-model-1#AccumulatedEnergyTimesPrice", out var vOut);
                    Assert.True(!found || (string)vOut!.Value == "0");
                }

                Fitness.Fitness fitness = new(simulationPathAndTree.Item1.NodeItem) {
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
                Assert.Equal(count, paths.Count());
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