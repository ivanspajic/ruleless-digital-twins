using Models;

namespace Logic.SensorValueHandlers
{
    public interface ISensorValueHandler
    {
        public bool EvaluateConstraint(object sensorValue, Tuple<ConstraintOperator, string> constraint);

        public object GetObservablePropertyValueFromMeasuredPropertyValues(params object[] measuredPropertyValues);
    }
}
