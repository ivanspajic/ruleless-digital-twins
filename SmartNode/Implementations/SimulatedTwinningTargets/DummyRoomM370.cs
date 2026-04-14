using Femyou;
using Logic.Models.MapekModels;
using System.Collections.ObjectModel;
using System.Reflection;
using static Femyou.IModel;

namespace Implementations.SimulatedTwinningTargets
{
    public class DummyRoomM370 {
        private const int Seed = 10110111;
        private const string FmuModelPath = "SmartNode/Implementations/FMUs/roomM370.fmu";
        private const string FmuInstanceName = "DummyRoomM370";
        private const string RoomTemperatureParameterName = "RoomTemperature";
        private const string RoomHumidityParameterName = "RoomHumidity";
        private const string EnergyConsumptionParameterName = "EnergyConsumption";
        private const string HeaterParameterName = "Heater";
        private const string FloorHeatingParameterName = "FloorHeating";
        private const string DehumidifierParameterName = "Dehumidifier";
        private const int CycleDurationSeconds = 900;
        private const int SimulationFidelitySeconds = 100;

        private readonly Random _randomGenerator = new(Seed);
        private readonly string _fmuModelFullFilepath;

        private double _roomTemperature = 17.7;
        private double _roomHumidity = 10.2;
        private double _energyConsumption = 22.0;

        private int _heaterState = 0;
        private int _floorHeatingState = 0;
        private int _dehumidifierState = 0;
        private int _oldHeaterState = 0;
        private int _oldFloorHeatingState = 0;
        private int _oldDehumidifierState = 0;

        private IModel? _fmuModel;
        private IInstance? _fmuInstance;

        private static DummyRoomM370? _instance;

        private DummyRoomM370(){
            var rootDirectory = Directory.GetParent(Assembly.GetExecutingAssembly().Location)!.Parent!.Parent!.Parent!.Parent!.Parent!.FullName;
            _fmuModelFullFilepath = Path.GetFullPath(Path.Combine(rootDirectory, FmuModelPath));
        }

        public static DummyRoomM370 Instance {
            get {
                _instance ??= new DummyRoomM370();

                return _instance;
            }
        }

        // Used for Sensor access.
        public double RoomTemperature {
            get => _roomTemperature;
        }

        public double RoomHumidity {
            get => _roomHumidity;
        }

        public double EnergyConsumption {
            get => _energyConsumption;
        }

        // Used for Actuator access.
        public int HeaterState {
            set {
                _heaterState = value;
            }
        }

        public int FloorHeatingState {
            set {
                _floorHeatingState = value;
            }
        }

        public int DehumidifierState {
            set {
                _dehumidifierState = value;
            }
        }

        public void ExecuteFmu(double mapekExecutionDuration) {
            // Check if we already loaded the model.
            _fmuModel ??= Model.Load(_fmuModelFullFilepath, new Collection<UnsupportedFunctions>([UnsupportedFunctions.SetTime2]));

            // Check if we already loaded the instance.
            _fmuInstance ??= _fmuModel.CreateCoSimulationInstance(FmuInstanceName);
            _fmuInstance.Reset();

            var roomTemperature = _fmuModel.Variables[RoomTemperatureParameterName];
            var roomHumidity = _fmuModel.Variables[RoomHumidityParameterName];
            var energyConsumption = _fmuModel.Variables[EnergyConsumptionParameterName];
            var heaterParameter = _fmuModel.Variables[HeaterParameterName];
            var floorHeatingParameter = _fmuModel.Variables[FloorHeatingParameterName];
            var dehumidifierParameter = _fmuModel.Variables[DehumidifierParameterName];

            // Set the old actuator states to account for TT changes during MAPE-K cycle execution before the DT enacted its decision.
            _fmuInstance.StartTime(0, (i) => {
                i.WriteInteger((heaterParameter, _oldHeaterState));
                i.WriteInteger((floorHeatingParameter, _oldFloorHeatingState));
                i.WriteInteger((dehumidifierParameter, _oldDehumidifierState));

                return true;
            });

            // Advance time for the duration of the MAPE-K execution to simulate TT changes in the meantime.
            AdvanceFmuTimeInSteps(_fmuInstance, mapekExecutionDuration);

            // Set the new actuator states.
            _fmuInstance.WriteInteger((heaterParameter, _heaterState));
            _fmuInstance.WriteInteger((floorHeatingParameter, _floorHeatingState));
            _fmuInstance.WriteInteger((dehumidifierParameter, _dehumidifierState));

            // Keep the actuator states for the next cycle.
            _oldHeaterState = _heaterState;
            _oldFloorHeatingState = _floorHeatingState;
            _oldDehumidifierState = _dehumidifierState;

            // Advance time for the remainder of the cycle after MAPE-K execution.
            var cycleDurationDelta = CycleDurationSeconds - mapekExecutionDuration;
            AdvanceFmuTimeInSteps(_fmuInstance, cycleDurationDelta);

            // Assign outputs to getters.
            var roomTemperatureOutput = _fmuInstance.ReadReal(roomTemperature).ToArray()[0];
            var roomHumidityOutput = _fmuInstance.ReadReal(roomHumidity).ToArray()[0];
            var energyConsumptionOutput = _fmuInstance.ReadReal(energyConsumption).ToArray()[0];

            // Randomize the values to simulate a "real" environment. The randomization is seeded, so running this simulation with the same starting values
            // should produce deterministic outputs.
            var roomTemperatureDeviation = _randomGenerator.NextDouble() - 0.5;
            var roomHumidityDeviation = _randomGenerator.NextDouble() - 0.5;

            // Set the output properties for the next cycle.
            _roomTemperature = roomTemperatureOutput + roomTemperatureDeviation;
            _roomHumidity = roomHumidityOutput + roomHumidityDeviation;
            // Accumulate this since it's accumulated in the simulations.
            _energyConsumption = energyConsumptionOutput + _energyConsumption;

            _fmuInstance.Dispose();
            _fmuInstance = null;
            _fmuModel.Dispose();
            _fmuModel = null;
        }

        private static void AdvanceFmuTimeInSteps(IInstance fmuInstance, double mapekExecutionDuration) {
            if (mapekExecutionDuration >= SimulationFidelitySeconds) {
                var maximumSteps = (double)mapekExecutionDuration / SimulationFidelitySeconds;
                var maximumStepsRoundedDown = (int)Math.Floor(maximumSteps);
                var difference = maximumSteps - maximumStepsRoundedDown;

                for (var i = 0; i < maximumStepsRoundedDown; i++) {
                    fmuInstance.AdvanceTime(SimulationFidelitySeconds);
                }

                // Advance the remainder of time to stay true to the simulation duration.
                fmuInstance.AdvanceTime(difference);
            } else {
                fmuInstance.AdvanceTime(mapekExecutionDuration);
            }            
        }
    }
}
