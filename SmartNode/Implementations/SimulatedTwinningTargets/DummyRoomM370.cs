using Femyou;
using Logic.Models.MapekModels;
using Logic.Models.OntologicalModels;
using System.Collections.ObjectModel;
using System.Diagnostics;
using static Femyou.IModel;

namespace Implementations.SimulatedTwinningTargets
{
    public class DummyRoomM370 {
        private readonly Random _randomGenerator = new(); // TODO: make this seeded!

        private double _roomTemperature = 17.7;
        private double _roomHumidity = 10.2;
        private double _energyConsumption = 0.0;

        private static DummyRoomM370? _instance;

        private DummyRoomM370() { }

        public static DummyRoomM370 Instance {
            get {
                _instance ??= new DummyRoomM370();

                return _instance;
            }
        }

        public double RoomTemperature {
            get => _roomTemperature;
            set {

            }
        }

        public double RoomHumidity {
            get => _roomHumidity;
            set {

            }
        }

        public double EnergyConsumption {
            get => _energyConsumption;
            set {

            }
        }

        private void ExecuteFmu(FmuModel fmuModel, Simulation simulation, double simulationDurationSeconds = 0) {
            var model = Model.Load(fmuModel.Filepath, GetUnsupportedFMUFunctions());
            Debug.Assert(model != null, "Model is null after loading.");
            // We're only using one instance per FMU, so we can just use the path as name.
            var instanceName = fmuModel.Filepath;
            var fmuInstance = model.CreateCoSimulationInstance(instanceName);
            Debug.Assert(fmuInstance != null, "Instance is null after creation.");
            fmuInstance.StartTime(simulation.Index * simulationDurationSeconds, (i) => Initialization(simulation, model, i));

            // Run the simulation by executing ActuationActions.
            var fmuActuationInputs = new List<(string, string, object)>();

            // Add all ActuatorStates to the inputs for the FMU.
            foreach (var action in simulation.Actions) {
                string name;
                string type;
                object value;
                if (action is ActuationAction actuationAction) {
                    name = actuationAction.Actuator.ParameterName ?? actuationAction.Actuator.Name;
                    type = actuationAction.Actuator.Type!;
                    value = actuationAction.NewStateValue;
                } else {
                    var reconfigurationAction = (ReconfigurationAction)action;
                    // TODO: override here as well?
                    name = reconfigurationAction.ConfigurableParameter.Name;
                    type = reconfigurationAction.ConfigurableParameter.OwlType;
                    value = reconfigurationAction.NewParameterValue;
                }

                // Shave off the long name URIs from the instance model.
                var simpleName = MapekUtilities.GetSimpleName(name);
                fmuActuationInputs.Add((simpleName, type, value));
            }

            // Add all ActuatorStates to the inputs for the FMU.
            foreach (var action in simulation.Actions) {
                string name;
                string type;
                object value;
                if (action is ActuationAction actuationAction) {
                    name = actuationAction.Actuator.ParameterName ?? actuationAction.Actuator.Name;
                    type = actuationAction.Actuator.Type!;
                    value = actuationAction.NewStateValue;
                } else {
                    var reconfigurationAction = (ReconfigurationAction)action;
                    // TODO: override here as well?
                    name = reconfigurationAction.ConfigurableParameter.Name;
                    type = reconfigurationAction.ConfigurableParameter.OwlType;
                    value = reconfigurationAction.NewParameterValue;
                }

                // Shave off the long name URIs from the instance model.
                var simpleName = MapekUtilities.GetSimpleName(name);
                fmuActuationInputs.Add((simpleName, type, value));
            }

            _logger.LogInformation("Parameters: {p}", string.Join(", ", fmuActuationInputs.Select(i => i.ToString())));
            AssignSimulationInputsToParameters(model, fmuInstance, fmuActuationInputs);

            _logger.LogDebug("Tick");
            // Advance the FMU time for the duration of the simulation tick in steps of simulation fidelity.
            var maximumSteps = (double)simulationDurationSeconds / fmuModel.SimulationFidelitySeconds;
            var maximumStepsRoundedDown = (int)Math.Floor(maximumSteps);
            var difference = maximumSteps - maximumStepsRoundedDown;

            for (var i = 0; i < maximumStepsRoundedDown; i++) {
                fmuInstance.AdvanceTime(fmuModel.SimulationFidelitySeconds);
            }

            // Advance the remainder of time to stay true to the simulation duration.
            fmuInstance.AdvanceTime(difference);

            AssignPropertyCacheCopyValues(fmuInstance, simulation.PropertyCache!, model.Variables);
        }
        protected virtual bool Initialization(Simulation simulation, IModel model, IInstance fmuInstance) {
            var actions = simulation.InitializationActions.Select(action => (action.Actuator.ParameterName ?? MapekUtilities.GetSimpleName(action.Name), action.Actuator.Type!, action.NewStateValue)).ToList();
            AssignSimulationInputsToParameters(model, fmuInstance, actions);
            return true;
        }

        protected virtual Collection<UnsupportedFunctions> GetUnsupportedFMUFunctions() {
            return new Collection<UnsupportedFunctions>([UnsupportedFunctions.SetTime2]);
        }
    }
}
