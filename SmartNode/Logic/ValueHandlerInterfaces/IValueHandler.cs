using Femyou;
using Logic.Models.MapekModels;
using Logic.Models.OntologicalModels;

namespace Logic.ValueHandlerInterfaces
{
    public interface IValueHandler
    {
        public IEnumerable<AtomicConstraintExpression> GetUnsatisfiedConstraintsFromEvaluation(ConstraintExpression constraintExpression, object propertyValue);

        public object GetObservablePropertyValueFromMeasuredPropertyValues(params object[] measuredPropertyValues);

        public IEnumerable<object> GetPossibleValuesForReconfigurationAction(ConfigurableParameter configurableParameter, int simulationGranularity, Effect effect);

        public bool IsGreaterThan(object comparingValue, object targetValue);

        public bool IsLessThan(object comparingValue, object targetValue);

        public object GetValueFromSimulationParameter(IInstance fmuInstance, IVariable parameter);

        public void WriteValueToSimulationParameter(IInstance fmuInstance, IVariable parameter, object value);
    }
}
