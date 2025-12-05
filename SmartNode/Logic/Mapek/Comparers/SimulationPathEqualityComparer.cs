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
                var xSimulations = x.Simulations.ToList();
                var ySimulations = y.Simulations.ToList();

                for (var i = 0; i < xSimulations!.Count; i++)
                {
                    if (xSimulations[i].Index == ySimulations![i].Index &&
                        xSimulations[i].ActuationActions.Count() == ySimulations[i].ActuationActions.Count())
                    {
                        var xSimulationActionsToExecute = xSimulations[i].ActuationActions.ToList();
                        var ySimulationActionsToExecute = ySimulations[i].ActuationActions.ToList();

                        foreach (var xSimulationActionToExecute in xSimulationActionsToExecute!)
                        {
                            if (!ySimulationActionsToExecute!.Contains(xSimulationActionToExecute, _actionEqualityComparer))
                            {
                                return false;
                            }
                        }

                        foreach (var ySimulationActionToExecute in ySimulationActionsToExecute!)
                        {
                            if (!xSimulationActionsToExecute.Contains(ySimulationActionToExecute, _actionEqualityComparer))
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
