using Implementations.SimulatedTwinningTargets;
using Logic.TTComponentInterfaces;
using System.Diagnostics;

namespace Implementations.Sensors.Incubator {
    public class AMQSensor(string sensorName, string procedureName, Func<IncubatorFields, double> f) : ISensor {
        private readonly IncubatorAdapter _incubatorAdapter = IncubatorAdapter.GetInstance("localhost", new CancellationToken());

        public bool _onceOnly = true;

        public string SensorName { get; private init; } = sensorName;

        public string ProcedureName { get; private init; } = procedureName;

        public object ObservePropertyValue(params object[] inputProperties) {
            IncubatorFields? myData = null;
            Monitor.Enter(_incubatorAdapter);
            myData = _incubatorAdapter.Data;
            Monitor.Exit(_incubatorAdapter);
            Debug.Assert(myData != null, "No data received from Incubator AMQP.");
            _onceOnly = false;
            return f(myData);
        }
    }
}
