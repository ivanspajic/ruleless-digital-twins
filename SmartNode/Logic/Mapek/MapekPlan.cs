using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Models;
using Models.MapekModels;

namespace Logic.Mapek
{
    public class MapekPlan : IMapekPlan
    {
        private readonly ILogger<MapekPlan> _logger;

        public MapekPlan(IServiceProvider serviceProvider)
        {
            _logger = serviceProvider.GetRequiredService<ILogger<MapekPlan>>();
        }

        public Plan Plan(Tuple<List<Mitigation>, List<Models.Action>> mitigationAndOptimizationTuple)
        {
            _logger.LogInformation("Starting the Plan phase.");

            // 1. run simulations for all actions present
                // this should be done with some heuristics. spawn them all but don't run them all for the entire
                // duration of the simulation (e.g., 1h). cut this up into smaller, granular chunks, and then
                // continue with those that seem to get closer. we could outsource this deciding logic via a
                // user-defined delegate
            // 2. check the results of all remaining simulations at the end of the whole duration (e.g., 1h) and
            // pick those whose results meet their respective optimalconditions
            // 3. put them in a plan and return the plan for execution
                // the plan should have an execution duration limit linked to the maximum amount of time required for
                // reaching the relevant optimalconditions

            

            return new Plan()
            {
                Actions = new List<Models.Action>()
                {
                    new ActuationAction
                    {
                        ActuatorState = new ActuatorState
                        {
                            Actuator = "Heater",
                            Name = "something"
                        },
                        Name = "something"
                    },
                    new ReconfigurationAction
                    {
                        ConfigurableParameter = new ConfigurableParameter
                        {
                            LowerLimitValue = 0.5,
                            Name = "http://www.semanticweb.org/ispa/ontologies/2025/instance-model-2/Epsilon",
                            OwlType = "double",
                            UpperLimitValue = 10.5,
                            Value = 19.5,
                            ValueIncrements = 0.5
                        },
                        Effect = Effect.ValueIncrease,
                        Name = "something",
                        AltersBy = 12.52
                    }
                }
            };
        }


    }
}
