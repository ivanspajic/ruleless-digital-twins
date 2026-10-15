using System.Diagnostics;
using System.Reflection;
using Fitness;
using Logic.FactoryInterface;
using Logic.Mapek;
using Logic.Models.MapekModels;
using Logic.Models.OntologicalModels;
using TestProject.Mocks.ServiceMocks;

namespace TestProject
{
    public class FitnessTests
    {
        [Fact]
        public async Task FitnessTest()
        {
            IRDTServiceProvider serviceProvider = new ServiceProviderMock();

            var rootDirectory = Directory.GetParent(Assembly.GetExecutingAssembly().Location)!.Parent!.Parent!.Parent!.Parent!.Parent!.FullName;
            var filepathArguments = new FilepathArguments
            {
                DataDirectory = "",
                FmuDirectory = Path.GetFullPath(Path.Combine(rootDirectory, "SmartNode", "Implementations", "FMUs")),
                InferenceEngineFilepath = Path.GetFullPath(Path.Combine(rootDirectory, "models-and-rules", "ruleless-digital-twins-inference-engine.jar")),
                InferenceRulesFilepath = Path.GetFullPath(Path.Combine(rootDirectory, "models-and-rules", "inference-rules.rules")),
                InferredModelFilepath = Path.GetFullPath(Path.Combine(rootDirectory, "models-and-rules", "M370-simulation-inferred.ttl")),
                InstanceModelFilepath = Path.GetFullPath("/tmp/M370.ttl"),
                OntologyFilepath = Path.GetFullPath(Path.Combine(rootDirectory, "ontology", "ruleless-digital-twins.ttl"))
            };
            serviceProvider.Add(filepathArguments);
            // Refresh model (#70):
            var model = Path.GetFullPath(Path.Combine(rootDirectory, "models-and-rules", "M370.ttl"));
            File.Copy(model.ToString(), filepathArguments.InstanceModelFilepath.ToString(), true);

            var coordinatorSettings = new CoordinatorSettings
            {
                Environment = "roomM370",
                LookAheadMapekCycles = 2,
                MaximumMapekRounds = 1,
                PropertyValueFuzziness = 0.25,
                SaveMapekCycleData = false,
                CycleDurationSeconds = 900,
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

            var f_energy = new FProp("http://www.semanticweb.org/ivans/ontologies/2025/instance-model-1#EnergyConsumption", "xsd:double");
            var f_temp = new FProp("http://www.semanticweb.org/ivans/ontologies/2025/instance-model-1#price", "xsd:double");
            var f_prod = new FBinOpArith<double>(f_energy, f_temp, (x, y) => x * y, name: "http://www.semanticweb.org/ivans/ontologies/2025/instance-model-1#EnergyTimesPrice");
            var f_prod_acc = new FAcc<double>(f_prod, name: "http://www.semanticweb.org/ivans/ontologies/2025/instance-model-1#AccumulatedEnergyTimesPrice");
            var f_ite_cond = new FRelOp<double>(f_energy, new FConst<double>(0.0, "xsd:double"), (x, y) => x > y, name: "http://www.semanticweb.org/ivans/ontologies/2025/instance-model-1#EnergyGreaterThan100");
            var f_ite = new FITE<double>(f_ite_cond, f_prod_acc, f_prod_acc, name: "http://www.semanticweb.org/ivans/ontologies/2025/instance-model-1#EnergyIfEnergyGreaterThan0ElsePrice");

            MapekPlan plan = new MapekPlan(serviceProvider);
            plan.FitnessOps = [f_prod_acc, f_ite];
            plan._minMaxOverrides = false;
            serviceProvider.Add(plan);

            var cache = await monitor.Monitor(0);

            Stopwatch sw = Stopwatch.StartNew();
            var simulationPathAndTree = await plan.Plan(cache, 0);
            sw.Stop();
            Debug.WriteLine($"Planning took {sw.Elapsed.TotalSeconds} seconds total.");

            IEnumerable<SimulationPath> paths = plan.GetOptimalSimulationPath(cache, simulationPathAndTree.Item1.SimulationPaths);
            Debug.WriteLine($"{paths.First().Simulations.First().PropertyCache.Properties["http://www.semanticweb.org/ivans/ontologies/2025/instance-model-1#AccumulatedEnergyTimesPrice"].Value} ");
            Debug.WriteLine($"{paths.First().Simulations.First().PropertyCache.Properties["http://www.semanticweb.org/ivans/ontologies/2025/instance-model-1#EnergyIfEnergyGreaterThan0ElsePrice"].Value} ");
        }
    }

    class WrappedSim : IComparable
    {
        public required Fitness.Fitness Fitness { set; get; }
        public required Simulation Simulation { set; get; }
        public int CompareTo(object? obj)
        {
            if (obj is not WrappedSim other)
            {
                throw new ArgumentException("Object is not a WrappedSim");
            }
            var sim2 = (Simulation)obj;
            FOp[] fops = [];
            var f1 = new Fitness.Fitness(Simulation) { FOps = fops };
            var f2 = new Fitness.Fitness(sim2) { FOps = fops };
            var a1 = f1.Process(f1.MkState(), Simulation);
            var a2 = f2.Process(f2.MkState(), sim2);
            foreach (var fop in fops)
            {
                Property p1 = (Property)a1.Properties[fop.Prop.Name];
                Property p2 = (Property)a2.Properties[fop.Prop.Name];
                Debug.Assert(p1.OwlType.Equals(p2.OwlType));
                if (p1.OwlType == "xsd:double")
                {
                    double v1 = (double)p1.Value;
                    double v2 = (double)p2.Value;

                    if (v1 < v2)
                    {
                        return -1;
                    }
                    else if (v1 > v2)
                    {
                        return 1;
                    }
                    else
                    {
                        return 0;
                    }
                }
            }
        }
    }
}