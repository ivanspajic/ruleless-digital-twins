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
        public override IEnumerable<SimulationPath> GetOptimalSimulationPath(Cache cache,
                IEnumerable<SimulationPath> simulationPaths) {
            return GetOptimalSimulationPathsEuclidian(simulationPaths, cache.OptimalConditions).Select((s,d) => s.Item1);
        }

        internal IEnumerable<(SimulationPath, double)>? GetOptimalSimulationPathsEuclidian(IEnumerable<SimulationPath> simulationPaths, IEnumerable<OptimalCondition> optimalConditions) {
            var pathXdists = simulationPaths.Select(sp => {
                // We only look into the final state of the simulation:
                var lastPC = sp.Simulations.Last().PropertyCache;
                var distances = optimalConditions.Select(oc => {
                    Debug.Assert(lastPC.Properties.TryGetValue(oc.Property.Name, out var p));
                    Debug.Assert(p != null);
                    if (!"http://www.w3.org/2001/XMLSchema#double".Equals(oc.Property.OwlType) || oc.ConditionConstraint.ConstraintType == ConstraintType.Or) {
                        // Coward.
                        Debug.WriteLine("Euclid: Cowardly refusing do to anything here.");
                        return 1;
                    }
                    var vh = _factory.GetValueHandlerImplementation(p.OwlType);
                    // We normalise the distance from the "border", values off the chart ("far left/far right") get turned into 1.
                    if (oc.ConditionConstraint.ConstraintType == ConstraintType.And) {
                        // E.g. in Incubator
                        NestedConstraintExpression c = (NestedConstraintExpression)oc.ConditionConstraint;
                        var minC = (AtomicConstraintExpression)c.Left;
                        double minVal = double.Parse(minC.Property.Value.ToString()); // TODO: review why sometimes the model turns 30.0 into 30
                        if (vh.IsLessThanOrEqualTo(p.Value, minVal)) {
                            double v = minVal - (double)p.Value;
                            return Math.Min(1, Math.Abs(v / minVal));
                        }

                        var maxC = (AtomicConstraintExpression)c.Right;
                        double maxVal = double.Parse(maxC.Property.Value.ToString());
                        if (vh.IsGreaterThanOrEqualTo(p.Value, maxVal)) {
                            double v = (double)p.Value - maxVal;
                            return Math.Min(1, Math.Abs(v / maxVal));
                        }
                    }
                    // TODO: Review in light of above changes.
                    if (oc.ConditionConstraint.ConstraintType == ConstraintType.LessThan || oc.ConditionConstraint.ConstraintType == ConstraintType.LessThanOrEqualTo) {
                        var minC = (AtomicConstraintExpression)oc.ConditionConstraint;
                        if (vh.IsLessThanOrEqualTo(p.Value, minC.Property)) {
                            double v = double.Parse((string)minC.Property.Value) - (double)p.Value;
                            return Math.Min(1, Math.Abs(v / double.Parse((string)minC.Property.Value)));
                        }
                    }
                    if (oc.ConditionConstraint.ConstraintType == ConstraintType.GreaterThan || oc.ConditionConstraint.ConstraintType == ConstraintType.GreaterThanOrEqualTo) {
                        var maxC = (AtomicConstraintExpression)oc.ConditionConstraint;
                        if (vh.IsGreaterThanOrEqualTo(p.Value, maxC.Property)) {
                            double v = (double)p.Value - double.Parse((string)maxC.Property.Value);
                            return Math.Min(1, Math.Abs(v / double.Parse((string)maxC.Property.Value)));
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
