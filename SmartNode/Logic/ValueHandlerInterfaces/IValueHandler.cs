using Femyou;
using Logic.Models.MapekModels;

namespace Logic.ValueHandlerInterfaces
{
    public interface IValueHandler
    {
        public IEnumerable<AtomicConstraintExpression> GetUnsatisfiedConstraintsFromEvaluation(ConstraintExpression constraintExpression, object propertyValue);

        public object GetObservablePropertyValueFromMeasuredPropertyValues(params object[] measuredPropertyValues);

        public int IncreaseComp(object comparingValue, object targetValue);

        public bool IsGreaterThanOrEqualTo(object comparingValue, object targetValue);

        public bool IsLessThanOrEqualTo(object comparingValue, object targetValue);

        public object GetValueFromSimulationParameter(IInstance fmuInstance, IVariable parameter);

        public void WriteValueToSimulationParameter(IInstance fmuInstance, IVariable parameter, object value);

        public string GetValueAsCultureInvariantString(object value);

        public object GetQuantizedValue(object value, double fuzziness);
    }
}
