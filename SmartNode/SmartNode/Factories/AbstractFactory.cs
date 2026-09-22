using Implementations.ValueHandlers;
using Logic.TTComponentInterfaces;
using Logic.ValueHandlerInterfaces;

namespace SmartNode.Factories {
    public abstract class AbstractFactory{
        // Since sensors and actuators mostly relate to sensor-actuator networks as communciation media for physical TTs (PTs), this factory (and its subclasses) allows for
        // registering implementations that deliberately do not use the physical implementation as the TT. For testing purposes, one can thus register sensors and actuators for mock
        // environments (dummy environments) with the names of those environments as keys of the maps. Since ConfigurableParameters and value handlers aren't coupled to physical
        // systems, these can just be registered in one map.
        // 
        // New implementations can simply be added to the factory collections.
        private readonly IDictionary<(string, string), ISensor> _sensorMap;
        private readonly IDictionary<string, IActuator> _actuatorMap;
        private readonly IDictionary<string, IConfigurableParameter> _configurableParameterMap;
        private readonly IDictionary<string, IValueHandler> _valueHandlerMap;

        internal AbstractFactory(IServiceProvider serviceProvider) {
            _sensorMap = MakeSensorMap(serviceProvider);
            _actuatorMap = MakeActuatorMap(serviceProvider);
            _configurableParameterMap = MakeConfigurableParameterMap(serviceProvider);
            _valueHandlerMap = MakeValueHandlerMap();
        }

        public ISensor GetSensorImplementation(string sensorName, string procedureName) {
            if (_sensorMap.TryGetValue((sensorName, procedureName), out ISensor? sensor)) {
                return sensor;
            }

            throw new Exception($"No implementation was found for Sensor {sensorName} with Procedure {procedureName}.");
        }

        public IActuator GetActuatorImplementation(string actuatorName) {
            if (_actuatorMap.TryGetValue(actuatorName, out IActuator? actuator)) {
                return actuator;
            }

            throw new Exception($"No implementation was found for Actuator {actuatorName}.");
        }

        public IConfigurableParameter GetConfigurableParameterImplementation(string configurableParameterName) {
            if (_configurableParameterMap.TryGetValue(configurableParameterName, out IConfigurableParameter? configurableParameter)) {
                return configurableParameter;
            }

            throw new Exception($"No implementation was found for software component {configurableParameterName}.");
        }

        public IValueHandler GetValueHandlerImplementation(string owlType) {
            if (_valueHandlerMap.TryGetValue(owlType, out IValueHandler? valueHandler)) {
                return valueHandler;
            }

            throw new Exception($"No implementation was found for Sensor value handler for OWL type {owlType}.");
        }

        protected abstract IDictionary<(string, string), ISensor> MakeSensorMap(IServiceProvider serviceProvider);

        protected abstract IDictionary<string, IActuator> MakeActuatorMap(IServiceProvider serviceProvider);

        protected abstract IDictionary<string, IConfigurableParameter> MakeConfigurableParameterMap(IServiceProvider serviceProvider);

        // The keys represent the OWL (RDF/XSD) types supported by Protege, and the values are user implementations.
        protected static IDictionary<string, IValueHandler> MakeValueHandlerMap() {
            return new Dictionary<string, IValueHandler>() {
                { "http://www.w3.org/2001/XMLSchema#double", new DoubleValueHandler() },
                { "http://www.w3.org/2001/XMLSchema#string", new StringValueHandler() },
                { "http://www.w3.org/2001/XMLSchema#int", new IntValueHandler() },
                { "http://www.w3.org/2001/XMLSchema#integer", new IntValueHandler() },
                { "http://www.w3.org/2001/XMLSchema#base64Binary", new Base64BinaryValueHandler() },
                { "http://www.w3.org/2001/XMLSchema#boolean", new BooleanValueHandler() }
            };
        }
    }
}
