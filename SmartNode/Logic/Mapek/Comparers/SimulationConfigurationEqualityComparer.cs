using Logic.Models.MapekModels;
using System.Diagnostics.CodeAnalysis;

namespace Logic.Mapek.EqualityComparers
{
    internal class SimulationConfigurationEqualityComparer : IEqualityComparer<SimulationConfiguration>
    {
        private readonly ActionEqualityComparer _actionEqualityComparer = new();

        public bool Equals(SimulationConfiguration? x, SimulationConfiguration? y)
        {
            if (x!.SimulationTicks.Count() == y!.SimulationTicks.Count())
            {
                var xSimulationTicks = x.SimulationTicks.ToList();
                var ySimulationTicks = y.SimulationTicks.ToList();
                var xPostTickActions = x.PostTickActions.ToList();
                var yPostTickActions = y.PostTickActions.ToList();

                for (var i = 0; i < xSimulationTicks!.Count; i++)
                {
                    if (xSimulationTicks[i].TickIndex == ySimulationTicks![i].TickIndex &&
                        xSimulationTicks[i].TickDurationSeconds == ySimulationTicks[i].TickDurationSeconds &&
                        xSimulationTicks[i].ActionsToExecute.Count() == ySimulationTicks[i].ActionsToExecute.Count())
                    {
                        var xTickActionsToExecute = xSimulationTicks[i].ActionsToExecute.ToList();
                        var yTickActionsToExecute = ySimulationTicks[i].ActionsToExecute.ToList();

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

                foreach (var xPostTickAction in xPostTickActions!)
                {
                    if (!yPostTickActions!.Contains(xPostTickAction))
                    {
                        return false;
                    }
                }

                foreach (var yPostTickAction in yPostTickActions!)
                {
                    if (!xPostTickActions.Contains(yPostTickAction))
                    {
                        return false;
                    }
                }

                return true;
            }

            return false;
        }

        public int GetHashCode([DisallowNull] SimulationConfiguration obj)
        {
            var finalHashCode = 1;

            foreach (var simulationTick in obj.SimulationTicks)
            {
                finalHashCode *= simulationTick.TickIndex.GetHashCode() * simulationTick.TickDurationSeconds.GetHashCode();

                foreach (var action in simulationTick.ActionsToExecute)
                {
                    finalHashCode *= _actionEqualityComparer.GetHashCode(action);
                }
            }

            foreach (var action in obj.PostTickActions)
            {
                finalHashCode *= _actionEqualityComparer.GetHashCode(action);
            }

            return finalHashCode;
        }
    }
}
