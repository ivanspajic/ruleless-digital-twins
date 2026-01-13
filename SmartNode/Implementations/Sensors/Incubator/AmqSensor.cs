using Implementations.SimulatedTwinningTargets;
using Logic.TTComponentInterfaces;
using System.Diagnostics;

namespace Implementations.Sensors.Incubator {
    public class AmqSensor(IncubatorAdapter incubatorAdapter, string sensorName, string procedureName, Func<IncubatorFields, double> f) : ISensor {
        private readonly IncubatorAdapter _incubatorAdapter = incubatorAdapter;

        public bool _onceOnly = true;
        private const int IncubatorAdapterMessageDelayMilliseconds = 2_500;

        public string SensorName { get; private init; } = sensorName;

        public string ProcedureName { get; private init; } = procedureName;

        public async Task<object> ObservePropertyValue(params object[] inputProperties) {
            // Wait a little bit before messages are sent to the queue.
            await Task.Delay(IncubatorAdapterMessageDelayMilliseconds);

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
