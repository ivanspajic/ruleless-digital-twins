using Femyou;
using Logic.Models.MapekModels;
using Logic.Models.OntologicalModels;

namespace Logic.ValueHandlerInterfaces
{
    public interface IValueHandler
    {
        public IEnumerable<AtomicConstraintExpression> GetUnsatisfiedConstraintsFromEvaluation(ConstraintExpression constraintExpression, object propertyValue);

        public object GetObservablePropertyValueFromMeasuredPropertyValues(params object[] measuredPropertyValues);

        public IEnumerable<object> GetPossibleValuesForReconfigurationAction(ConfigurableParameter configurableParameter, Effect effect);

        public IEnumerable<object> GetPossibleValuesForActuationAction(Actuator actuator);

        public object GetInitialValueForConfigurableParameter(string configurableParameter);

        public int IncreaseComp(object comparingValue, object targetValue);

        public bool IsGreaterThanOrEqualTo(object comparingValue, object targetValue);

        public bool IsLessThanOrEqualTo(object comparingValue, object targetValue);

        public object GetValueFromSimulationParameter(IInstance fmuInstance, IVariable parameter);

        public void WriteValueToSimulationParameter(IInstance fmuInstance, IVariable parameter, object value);

        public string GetValueAsCultureInvariantString(object value);
    }
}
