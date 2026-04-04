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
                var f1 = new FAcc<double>(prop);
                var f2 = new FAvg<double>(prop);
                var f3 = new FBinOpSum(f1,f2);
                Fitness fitness = new(path.First()) {                    
                    FOps = new FOp[] { f1, f2, f3 }
                };

                var result = path.Skip(1).Aggregate(fitness.MkState(), fitness.Process);
                
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
    - the elements in the cache will be derived from the hash code of the operation which should make them unique.
    - TODO: binary operators where `Eval()` descends, and an if-then-else-like projection
    */
    internal class AccState {

        // We abuse the property-cache to keep state:
        public IDictionary<string, object> Properties { get; init; } = new Dictionary<string, object>() {};

        internal void Set(Property prop, object value) {
            Properties[prop.Name] = value;
        }

        internal object Get(Property prop) {
            object outP;
            Properties.TryGetValue(prop.Name, out outP);
            return outP;
        }

        public AccState(Fitness fitness) {
            foreach (var o in fitness.FOps) {
                // Could probably be nicer/Zip...
                var ivs = o.MkInitialValues().GetEnumerator();
                foreach (var p in o.MkProps()) {
                    ivs.MoveNext();
                    Set(p, ivs.Current);
                }
            }
        }
    }
    
    // Computer the average of a property.
    // TODO: Should probably inherit from Facc<>, but I didn't manage reuse of the superclass yet.
    class FAvg<T> : FOp where T : INumber<T> {
        int counter = 1;
        public FAvg(Property prop) {
            this.Orig = prop;
            this.Acc = new Property() { OwlType = prop.OwlType, Name = GetHashCode().ToString()+"_ACC", Value=null};
            // Output:
            this.Prop = new Property() { OwlType = prop.OwlType, Name = GetHashCode().ToString()+"_AVG", Value=null};
        }

        internal override IEnumerable<object> MkInitialValues() {
            return new[] {Orig.Value, Orig.Value};
        }

        internal override IEnumerable<Property> MkProps() {
            return new[] {Prop, Acc};
        }

        public override void Eval(AccState in_state, Simulation sim, AccState out_state) {
            counter++;
            out_state.Set(Acc, (T)in_state.Get(Acc) + (T)sim.PropertyCache.Properties[Orig.Name].Value);
            out_state.Set(Prop, (T)out_state.Get(Acc) / T.CreateChecked(counter));
        }

        Property Acc { get; }
        Property Orig { get; }
    }

    internal class Fitness {
        Simulation previous;        
        // We support multiple "root" expressions.
        required public IEnumerable<FOp> FOps { get; init;}

        public Fitness(Simulation simulation) {
            previous = simulation;
        }

        internal AccState Process(AccState state, Simulation simulation) {
            // Update `state` in place:
            FOps.ForEach(fop => fop.Eval(state, simulation, state));
            previous = simulation;
            return state;
        }

        internal AccState MkState() {
            return new AccState(this);
        }
    }

    abstract class FOp {
        public abstract void Eval(AccState in_state, Simulation sim, AccState out_state);
        internal abstract IEnumerable<object> MkInitialValues();
        internal abstract IEnumerable<Property> MkProps();
        // If we need a value, that's where it is:
        public Property Prop { get; set; }
    }

    abstract class FBinOp : FOp {
        public FBinOp(FOp left, FOp right) {
            this.L = left;
            this.R = right;
            // XXX Other datatypes...
            this.Prop = new Property() { OwlType = "http://www.w3.org/2001/XMLSchema#double", Name = GetHashCode().ToString()+"_BinOp", Value=null};
        }

        internal override IEnumerable<object> MkInitialValues() {
            return new object[] { (int)0 };
        }

        internal override IEnumerable<Property> MkProps() {
            return new[] { Prop };
        }

        public override void Eval(AccState in_state, Simulation sim, AccState out_state) {
            // Evaluate both sides independently:
            L.Eval(in_state, sim, out_state);
            R.Eval(in_state, sim, out_state);
            out_state.Set(Prop, Operation(out_state.Get(L.Prop), out_state.Get(R.Prop)));
        }

        // TODO: would be nice to have a type parameter here tied to the OwlType above:
        protected abstract object Operation(object v1, object v2);

        FOp L { get; }
        FOp R { get; }
    }

    class FBinOpSum : FBinOp {
        public FBinOpSum(FOp left, FOp right) : base(left, right) {}

        protected override object Operation(object v1, object v2) {
            return ((double) v1 + (double) v2);
        }
    }

    class FAcc<T> : FOp where T : INumber<T> {
        // We accumulate the values of a property by overriding `Operation`.
        // Construct "fake" property to hold accumulator value derived from the object id(!), not the name.
        public FAcc(Property prop) {
            this.Orig = prop;
            this.Prop = new Property() { OwlType = prop.OwlType, Name = GetHashCode().ToString()+"_ACC", Value=null};
        }

        internal override IEnumerable<object>  MkInitialValues() {
            // Explicitly fetch initial value
            return new[] {Orig.Value};
        }

        internal override IEnumerable<Property> MkProps() {
            return new[] {Prop};
        }

        public override void Eval(AccState in_state, Simulation sim, AccState out_state) {
             out_state.Set(Prop, (T)in_state.Get(Prop) + (T)sim.PropertyCache.Properties[Orig.Name].Value);
        }

        Property Orig { get; }
    }
}
