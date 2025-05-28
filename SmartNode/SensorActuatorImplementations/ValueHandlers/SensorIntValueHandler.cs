using Logic.SensorValueHandlers;
using Models;

namespace SensorActuatorImplementations.ValueHandlers
{
    // Example int Sensor value handler implementation.
    public class SensorIntValueHandler : ISensorValueHandler
    {
        public bool EvaluateConstraint(object sensorValue, Tuple<ConstraintOperator, string> constraint)
        {
            throw new NotImplementedException();
        }

        public object GetObservablePropertyValueFromMeasuredPropertyValues(params object[] measuredPropertyValues)
        {
            throw new NotImplementedException();
        }
    }
}
