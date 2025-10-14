﻿using Implementations.SimulatedTwinningTargets;
using Logic.DeviceInterfaces;

namespace Implementations.Sensors
{
    public class DummyTemperatureSensor : ISensorDevice
    {
        private readonly DummyRoomM370 _dummyRoomM370;

        public DummyTemperatureSensor(string sensorName, string procedureName, DummyRoomM370 dummyRoomM370)
        {
            SensorName = sensorName;
            ProcedureName = procedureName;
            _dummyRoomM370 = dummyRoomM370;
        }

        public string SensorName { get; private set; }

        public string ProcedureName { get; private set; }

        public object ObservePropertyValue(params object[] inputProperties)
        {
            return _dummyRoomM370.RoomTemperature;
        }
    }
}
