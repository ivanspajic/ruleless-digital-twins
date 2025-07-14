using Logic.Models.MapekModels;
using Logic.Models.OntologicalModels;
using Logic.ValueHandlerInterfaces;

namespace SensorActuatorImplementations.ValueHandlers
{
    public class ExampleTimespanValueHandler : IValueHandler
    {
        public object GetObservablePropertyValueFromMeasuredPropertyValues(params object[] measuredPropertyValues)
        {
            throw new NotImplementedException();
        }

        public IEnumerable<object> GetPossibleValuesForReconfigurationAction(object currentValue,
            object minimumValue,
            object maximumValue,
            int simulationGranularity,
            Effect effect,
            string configurableParameterName)
        {
            throw new NotImplementedException();
        }

        public IEnumerable<AtomicConstraintExpression> GetUnsatisfiedConstraintsFromEvaluation(ConstraintExpression constraintExpression, object propertyValue)
        {
            throw new NotImplementedException();
        }
    }
}
