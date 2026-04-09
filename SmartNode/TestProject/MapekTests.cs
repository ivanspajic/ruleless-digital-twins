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

namespace TestProject {
    public class MapekTests {
        [Theory]
        [InlineData("instance-model-1.ttl", "inferred-model-1.ttl")]
        [InlineData("M370-instance.ttl","M370-inferred.ttl")]
        public async Task TestMapeK(String instance, String inferred) {
            // Arrange
            var serviceProvider = new ServiceProviderMock();

            var rootDirectory = Directory.GetParent(Assembly.GetExecutingAssembly().Location)!.Parent!.Parent!.Parent!.Parent!.Parent!.FullName;
            var filepathArguments = new FilepathArguments {
                DataDirectory = "",
                FmuDirectory = Path.GetFullPath(Path.Combine(rootDirectory, "SmartNode", "Implementations", "FMUs")),
                InferenceEngineFilepath = Path.GetFullPath(Path.Combine(rootDirectory, "models-and-rules", "ruleless-digital-twins-inference-engine.jar")),
                InferenceRulesFilepath = Path.GetFullPath(Path.Combine(rootDirectory, "models-and-rules", "inference-rules.rules")),
                InferredModelFilepath = Path.GetFullPath(Path.Combine(rootDirectory, "models-and-rules", inferred)),
                InstanceModelFilepath = Path.GetFullPath(Path.Combine(rootDirectory, "models-and-rules", instance)),
                OntologyFilepath = Path.GetFullPath(Path.Combine(rootDirectory, "ontology", "ruleless-digital-twins.ttl"))
            };
            serviceProvider.Add(filepathArguments);

            var coordinatorSettings = new CoordinatorSettings {
                Environment = "roomM370",
                LookAheadMapekCycles = 1,
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

            IMapekPlan plan = new MapekPlan(serviceProvider);
            serviceProvider.Add(plan);

            // Act
            try {
                var cache = await monitor.Monitor();
                { // Tweak cache:
                    cache.PropertyCache.Properties.Add("http://www.semanticweb.org/ivans/ontologies/2025/instance-model-1#price",
                        new Property { Name = "http://www.semanticweb.org/ivans/ontologies/2025/instance-model-1#price", Value = "0", OwlType = "http://www.w3.org/2001/XMLSchema#double" });
                }
                var simulationPathAndTree = await plan.Plan(cache);
                var path = simulationPathAndTree.Item2.Simulations;

                // Test property we want to accumulate:
                var f_energy = new FProp("http://www.semanticweb.org/ivans/ontologies/2025/instance-model-1#EnergyConsumption");
                var f_temp = new FProp("http://www.semanticweb.org/ivans/ontologies/2025/instance-model-1#price");
                var f_prod = new FBinOpArith(f_energy, f_temp, (x, y) => x * y, name: "http://www.semanticweb.org/ivans/ontologies/2025/instance-model-1#EnergyTimesPrice");
                var f_prod_acc = new FAcc<double>(f_prod, name: "http://www.semanticweb.org/ivans/ontologies/2025/instance-model-1#AccumulatedEnergyTimesPrice");
                
                Fitness.Fitness fitness = new(simulationPathAndTree.Item1.NodeItem) {                    
                    FOps = new FOp[] { f_prod, f_prod_acc }
                };

                var result = path.Aggregate(fitness.MkState(), fitness.Process);

                Debug.WriteLine(string.Join(",", fitness.FOps.Select(fop => fop.Prop.Name + "=" + result.Get(fop.Prop))));
            }
            catch (Exception exception) {
                Debug.WriteLine(exception.Message);
                throw;
            }

            // Assert
            Assert.True(true);
        }
    }
}
