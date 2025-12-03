using Logic.Models.MapekModels;
using System.Diagnostics.CodeAnalysis;

namespace Logic.Mapek.Comparers
{
    internal class SimulationPathEqualityComparer : IEqualityComparer<SimulationPath>
    {
        private readonly ActionEqualityComparer _actionEqualityComparer = new();

        public bool Equals(SimulationPath? x, SimulationPath? y)
        {
            if (x!.Simulations.Count() == y!.Simulations.Count())
            {
                var xSimulationTicks = x.Simulations.ToList();
                var ySimulationTicks = y.Simulations.ToList();

                for (var i = 0; i < xSimulationTicks!.Count; i++)
                {
                    if (xSimulationTicks[i].Index == ySimulationTicks![i].Index &&
                        xSimulationTicks[i].ActuationActions.Count() == ySimulationTicks[i].ActuationActions.Count())
                    {
                        var xTickActionsToExecute = xSimulationTicks[i].ActuationActions.ToList();
                        var yTickActionsToExecute = ySimulationTicks[i].ActuationActions.ToList();

                        foreach (var xTickActionToExecute in xTickActionsToExecute!)
                        {
                            if (!yTickActionsToExecute!.Contains(xTickActionToExecute, _actionEqualityComparer))
                            {
                                return false;
                            }
                        }

                        foreach (var yTickActionToExecute in yTickActionsToExecute!)
                        {
                            if (!xTickActionsToExecute.Contains(yTickActionToExecute, _actionEqualityComparer))
                            {
                                return false;
                            }
                        }
                    }
                    else
                    {
                        return false;
                    }
                }

                return true;
            }

            return false;
        }

        public int GetHashCode([DisallowNull] SimulationPath obj)
        {
            var finalHashCode = 1;

            foreach (var simulationTick in obj.Simulations)
            {
                finalHashCode *= simulationTick.Index.GetHashCode();

                foreach (var action in simulationTick.ActuationActions)
                {
                    finalHashCode *= _actionEqualityComparer.GetHashCode(action);
                }
            }

            return finalHashCode;
        }
    }
}
