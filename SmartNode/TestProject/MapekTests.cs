using Logic.FactoryInterface;
using Logic.Mapek;
using Logic.Models.MapekModels;
using Logic.Models.OntologicalModels;
using System.Diagnostics;
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
                if (instance == "M370-instance.ttl") { // Adjust cache for extended model :-}
                    cache.PropertyCache.Properties.Add("http://www.semanticweb.org/ivans/ontologies/2025/instance-model-1#price",
                        new Property { Name = "http://www.semanticweb.org/ivans/ontologies/2025/instance-model-1#price", Value = "0", OwlType = "http://www.w3.org/2001/XMLSchema#double" });
                }
                var simulationPathAndTree = await plan.Plan(cache);
                var path = simulationPathAndTree.Item2.Simulations;

                // Test property we want to accumulate:
                Property prop = cache.PropertyCache.Properties["http://www.semanticweb.org/ivans/ontologies/2025/instance-model-1#AverageTemperature"];
                Fitness<AccState> fitness = new(path.First()) {                    
                    FOps = new[] { new FAccFloat(prop) }
                };

                var result = path.Skip(1).Aggregate(new AccState(fitness, prop), fitness.Process);
                
                Debug.WriteLine(string.Join(",",result.Fitness.Properties));

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

    /* This is now a horrible mix of functional and imperative code:
    - in principle Aggregate() would handle the state for us
    - but we've now constructed the whole mess in such a way that the generic part is taken care off by destructively modifying the
        PropertyCache with our elements derived from the structure of the FOp...
    */
    internal class AccState : FOp<AccState> {
        int depth = 0;

        public Fitness<AccState> Fitness { set; get; }

        public AccState(Fitness<AccState> fitness, Property prop) {
            this.Fitness = fitness;
            fitness.Set(new Property(){ Name = prop.Name+"_ACC", OwlType = prop.OwlType, Value=null}, prop.Value);
        }

        public override void Eval(Fitness<AccState> fitness, Simulation sim) {
            depth++;
        }
    }
    
    internal class FAccFloat(Property prop) : FAcc<AccState>(prop) {
        public override object Operation(object v, object value) { return (double) v + (double) value; }
    }

    internal class Fitness<T> {
        Simulation previous;
        // We abuse the property-cache to keep state:
        public IDictionary<string, object> Properties { get; init; } = new Dictionary<string, object>() {};
        // We support multiple "root" expressions.
        required public IEnumerable<FOp<T>> FOps { get; init;}

        public Fitness(Simulation simulation) {
            previous = simulation;
        }

        internal T Process(T state, Simulation simulation) {
            FOps.ForEach(fop => fop.Eval(this, simulation));
            previous = simulation;
            return state; // XXX             
        }

        internal void Set(Property prop, object value) {
            Properties[prop.Name] = value;
        }

        internal object Get(Property prop) {
            object outP = null;
            Properties.TryGetValue(prop.Name, out outP);
            return outP;
        }
}

    abstract class FOp<T> {
        public abstract void Eval(Fitness<T> fitness, Simulation sim);
    }
    abstract class FAcc<T> : FOp<T> {
        // We accumulate the values of a property by overriding `Operation`.
        // Construct "fake" property to hold accumulator value
        public FAcc(Property prop) {
            this.Prop = prop;
            this.Acc = new Property() { OwlType = prop.OwlType, Name = Prop.Name+"_ACC", Value=null}; // XXX Unsafe name construction
        }

        public override void Eval(Fitness<T> fitness, Simulation sim) {
             fitness.Set(Acc, Operation(fitness.Get(Acc), sim.PropertyCache.Properties[Prop.Name].Value));
        }

        public abstract object Operation(object v, object value);

        public Property Prop { get; }
        public Property Acc { get; }
    }
}
