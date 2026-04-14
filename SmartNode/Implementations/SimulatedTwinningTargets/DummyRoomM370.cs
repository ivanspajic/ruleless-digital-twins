using Femyou;
using System.Collections.ObjectModel;
using static Femyou.IModel;

namespace Implementations.SimulatedTwinningTargets
{
    public class DummyRoomM370 {
        private const int Seed = 10110111;
        private const string FmuModelFilepath = "";
        private const string FmuInstanceName = "DummyRoomM370";
        private const string HeaterParameterName = "HeaterState";
        private const string FloorHeatingParameterName = "FloorHeatingState";
        private const string DehumidifierParameterName = "DehumidifierState";
        private const int CycleDurationSeconds = 900;

        private readonly Random _randomGenerator = new(Seed);

        private double _roomTemperature = 17.7;
        private double _roomHumidity = 10.2;
        private double _energyConsumption = 0.0;

        private int _heaterState = 0;
        private int _floorHeatingState = 0;
        private int _dehumidifierState = 0;

        private IModel? _fmuModel;
        private IInstance? _fmuInstance;

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

        public int HeaterState {
            get => _heaterState;
            set {
                _heaterState = value;
            }
        }

        public int FloorHeatingState {
            get => _floorHeatingState;
            set {
                _floorHeatingState = value;
            }
        }

        public int DehumidifierState {
            get => _dehumidifierState;
            set {
                _dehumidifierState = value;
            }
        }

        private void ExecuteFmu(double mapekExecutionDuration) {
            // Check if we already loaded the model.
            _fmuModel ??= Model.Load(FmuModelFilepath, new Collection<UnsupportedFunctions>([UnsupportedFunctions.SetTime2]));

            // Check if we already loaded the instance.
            _fmuInstance ??= _fmuModel.CreateCoSimulationInstance(FmuInstanceName);
            _fmuInstance.Reset();

            var heaterParameter = 

            // write the old actuator states (save them somewhere!!)
            _fmuInstance.StartTime(0, (i) => {
                i.WriteInteger((HeaterParameterName, HeaterState));

                return true;
            });

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
