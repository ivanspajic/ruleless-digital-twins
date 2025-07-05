using Logic.SensorValueHandlers;
using Logic.Models.OntologicalModels;

namespace SensorActuatorImplementations.ValueHandlers
{
    // Example int Sensor value handler implementation.
    public class SensorIntValueHandler : ISensorValueHandler
    {
        public object ChangeValueByAmount(object value, object amountToChangeBy, Effect typeOfChange)
        {
            throw new NotImplementedException();
        }

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
