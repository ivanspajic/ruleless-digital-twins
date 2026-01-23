using Logic.FactoryInterface;
using Logic.Models.MapekModels;
using Logic.Models.OntologicalModels;
using Microsoft.Extensions.DependencyInjection;
using System.Diagnostics;

namespace Logic.Mapek {
    public class EuclidMapekPlan : MapekPlan {
        private readonly IFactory _factory;

        public EuclidMapekPlan(IServiceProvider serviceProvider) : base(serviceProvider) {
            _factory = serviceProvider.GetRequiredService<IFactory>();
        }
        protected override SimulationPath GetOptimalSimulationPath(PropertyCache propertyCache,
                IEnumerable<Condition> conditions,
                IEnumerable<SimulationPath> simulationPaths) {
            return GetOptimalSimulationPathsEuclidian(simulationPaths, conditions).First().Item1;
        }

        internal IEnumerable<(SimulationPath, double)>? GetOptimalSimulationPathsEuclidian(IEnumerable<SimulationPath> simulationPaths, IEnumerable<Condition> conditions) {
            var pathXdists = simulationPaths.Select(sp => {
                // We only look into the final state of the simulation:
                var lastPC = sp.Simulations.Last().PropertyCache;
                var distances = conditions.Select(oc => {
                    Debug.Assert(lastPC.Properties.TryGetValue(oc.Property.Name, out var p));
                    Debug.Assert(p != null);
                    if (!"http://www.w3.org/2001/XMLSchema#double".Equals(oc.Property.OwlType) || oc.Constraints.Any(c => c.ConstraintType == ConstraintType.And || c.ConstraintType == ConstraintType.Or)) {
                        // Coward.
                        Debug.WriteLine("Cowardly refusing do to anything here.");
                        return 1;
                    }
                    var max = oc.Constraints.Where(c => c.ConstraintType == ConstraintType.LessThan || c.ConstraintType == ConstraintType.LessThanOrEqualTo);
                    var min = oc.Constraints.Where(c => c.ConstraintType == ConstraintType.GreaterThan || c.ConstraintType == ConstraintType.GreaterThanOrEqualTo);
                    Debug.WriteLineIf(max.Count() > 1 || min.Count() > 1, "Uh.");
                    var vh = _factory.GetValueHandlerImplementation(p.OwlType);
                    // We normalise the distance from the "border", values off the chart ("far left/far right") get turned into 1.
                    if (min.Count() == 1) {
                        var minC = (AtomicConstraintExpression)min.First();
                        if (vh.IsLessThanOrEqualTo(p.Value, minC.Right)) {
                            double v = double.Parse((string)minC.Right) - (double)p.Value;
                            return Math.Min(1, Math.Abs(v / double.Parse((string)minC.Right)));
                        }
                    }
                    if (max.Count() == 1) {
                        var maxC = (AtomicConstraintExpression)max.First();
                        if (vh.IsGreaterThanOrEqualTo(p.Value, maxC.Right)) {
                            double v = (double)p.Value - double.Parse((string)maxC.Right);
                            return Math.Min(1, Math.Abs(v / double.Parse((string)maxC.Right)));
                        }
                    }
                    return 0; // Fallthrough, we're in the interval!
                });
                // Euclidean distance from (0,..,0):
                return (sp, Math.Sqrt(distances.Sum(d => d * d)));
            });
            return pathXdists.OrderBy(pd => pd.Item2).ToList();
        }
    }
}
