namespace Logic.Utilities
{
    internal class SensorValueHandlerFactory
    {
        // Contains supported XMl/RDF/OWL types and their respective value handlers. As new types
        // become supported, their name/implementation instance pair is simply added to the collection.
        private static readonly Dictionary<string, ISensorValueHandler> _sensorValueHandlers = new()
        {
            { "int", new SensorIntValueHandler() },
            { "double", new SensorDoubleValueHandler() }
        };

        public static ISensorValueHandler GetSensorValueHandler(string valueType)
        {
            if (!_sensorValueHandlers.ContainsKey(valueType))
                return null!;

            return _sensorValueHandlers[valueType];
        }
    }
}
