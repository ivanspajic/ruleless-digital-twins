using Femyou;
using Logic.FactoryInterface;
using Logic.Mapek;
using Logic.Models.MapekModels;
using System.Diagnostics;
using System.Reflection;
using TestProject.Mocks.ServiceMocks;

namespace TestProject {
    public class MapekTests {
        [Fact]
        public async Task TestMapeK() {
            // Arrange
            var serviceProvider = new ServiceProviderMock();

            var rootDirectory = Directory.GetParent(Assembly.GetExecutingAssembly().Location)!.Parent!.Parent!.Parent!.Parent!.Parent!.FullName;
            var filepathArguments = new FilepathArguments {
                DataDirectory = "",
                FmuDirectory = Path.GetFullPath(Path.Combine(rootDirectory, "SmartNode", "Implementations", "FMUs")),
                InferenceEngineFilepath = Path.GetFullPath(Path.Combine(rootDirectory, "models-and-rules", "ruleless-digital-twins-inference-engine.jar")),
                InferenceRulesFilepath = Path.GetFullPath(Path.Combine(rootDirectory, "models-and-rules", "inference-rules.rules")),
                InferredModelFilepath = Path.GetFullPath(Path.Combine(rootDirectory, "models-and-rules", "inferred-model-1.ttl")),
                InstanceModelFilepath = Path.GetFullPath(Path.Combine(rootDirectory, "models-and-rules", "instance-model-1.ttl")),
                OntologyFilepath = Path.GetFullPath(Path.Combine(rootDirectory, "ontology", "ruleless-digital-twins.ttl"))
            };
            serviceProvider.Add(filepathArguments);

            var coordinatorSettings = new CoordinatorSettings {
                Environment = "roomM370",
                LookAheadMapekCycles = 2,
                MaximumMapekRounds = 4,
                PropertyValueFuzziness = 0.25,
                SaveMapekCycleData = false,
                CycleDurationSeconds = 3600,
                SleepyTimeMilliseconds = 0,
                StartInReactiveMode = false,
                UseCaseBasedFunctionality = false,
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
                var simulationPathAndTree = await plan.Plan(cache);

                Assert.Equal(simulationPathAndTree.Item2.Simulations.Count(), coordinatorSettings.LookAheadMapekCycles);
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
