using Logic.Models.MapekModels;
using Logic.Models.OntologicalModels;

namespace Logic.ValueHandlerInterfaces
{
    public interface IValueHandler
    {
        public IEnumerable<AtomicConstraintExpression> GetUnsatisfiedConstraintsFromEvaluation(ConstraintExpression constraintExpression);

        public object GetObservablePropertyValueFromMeasuredPropertyValues(params object[] measuredPropertyValues);

        public IEnumerable<object> GetPossibleValuesForReconfigurationAction(object currentValue,
            object minimumValue,
            object maximumValue,
            int simulationGranularity,
            Effect effect,
            string configurableParameterName);
    }
}
