using Femyou;
using System.Collections.ObjectModel;
using static Femyou.IModel;

namespace Implementations.SimulatedTwinningTargets
{
    public class DummyRoomM370 {
        private const int Seed = 10110111;
        private const string FmuModelFilepath = "";
        private const string FmuInstanceName = "DummyRoomM370";
        private const int CycleDurationSeconds = 900;

        private readonly Random _randomGenerator = new(Seed);

        private double _roomTemperature = 17.7;
        private double _roomHumidity = 10.2;
        private double _energyConsumption = 0.0;

        private IModel fmuModel;
        private IInstance fmuInstance;

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
                _roomTemperature = value;
            }
        }

        public double RoomHumidity {
            get => _roomHumidity;
            set {
                _roomHumidity = value;
            }
        }

        public double EnergyConsumption {
            get => _energyConsumption;
            set {
                _energyConsumption = value;
            }
        }

        private void ExecuteFmu(double simulationDurationSeconds) {
            // check if the model is already loaded
            var model = Model.Load(FmuModelFilepath, new Collection<UnsupportedFunctions>([UnsupportedFunctions.SetTime2]));
            // We're only using one instance per FMU, so we can just use the path as name.

            // check if the instance is already loaded
            var fmuInstance = model.CreateCoSimulationInstance(FmuInstanceName);

            // write the old parameters first (save them somewhere!!)
            // write the old actuator states (save them somewhere!!)
            fmuInstance.StartTime(0, (i) => i.WriteReal((parameter, (double)value));

            // Advance time for the duration of 
            var maximumSteps = (double)simulationDurationSeconds / fmuModel.SimulationFidelitySeconds;
            var maximumStepsRoundedDown = (int)Math.Floor(maximumSteps);
            var difference = maximumSteps - maximumStepsRoundedDown;

            for (var i = 0; i < maximumStepsRoundedDown; i++) {
                fmuInstance.AdvanceTime(fmuModel.SimulationFidelitySeconds);
            }

            // Advance the remainder of time to stay true to the simulation duration.
            fmuInstance.AdvanceTime(difference);

            // now write the new actuator states
            // advance time for the remainder of the cycle after simulation duration

            // Get values out here.
            fmuInstance.ReadReal(parameter).ToArray()[0];

            // Use randomization for temp and humid!! seed!! (en. cons. is almost the same)

            // set the properties so sensors can get em in the next cycle

        }
    }
}
