using Implementations.SimulatedTwinningTargets;
using Logic.DeviceInterfaces;

namespace Implementations.Actuators
{
    public class DummyAirConditioningUnit : IActuatorDevice
    {
        private int _actuatorState = 0;
        private readonly DummyRoomM370 _dummyRoomM370;

        public DummyAirConditioningUnit(string actuatorName, DummyRoomM370 dummyRoomM370)
        {
            ActuatorName = actuatorName;
            _dummyRoomM370 = dummyRoomM370;
        }

        public string ActuatorName { get; }

        public void Actuate(object state)
        {
            _actuatorState = (int)state;

            // The dummy Actuator doesn't represent the differential equations found in the
            // respective FMU. These states are simplifications adjusted for 900s of actuation.            
            switch (_actuatorState)
            {
                case 3:
                    _dummyRoomM370.RoomTemperature += 15;
                    break;

                case 2:
                    _dummyRoomM370.RoomTemperature += 10;
                    break;

                case 1:
                    _dummyRoomM370.RoomTemperature += 5;
                    break;

                default:
                    // Simply touch the property to activate the "cooling" mechanism in the dummy environment.
                    _dummyRoomM370.RoomTemperature += 0;
                    break;
            }
        }
    }
}
