using Logic.Models.MapekModels;
using Logic.Models.OntologicalModels;
using Logic.ValueHandlerInterfaces;

namespace SensorActuatorImplementations.ValueHandlers
{
    // Example int Sensor value handler implementation.
    public class ExampleIntValueHandler : IValueHandler
    {
        public object ChangeValueByAmount(object value, object amountToChangeBy, Effect typeOfChange)
        {
            throw new NotImplementedException();
        }

        public object GetObservablePropertyValueFromMeasuredPropertyValues(params object[] measuredPropertyValues)
        {
            throw new NotImplementedException();
        }

        public IEnumerable<AtomicConstraintExpression> GetUnsatisfiedConstraintsFromEvaluation(ConstraintExpression constraintExpression)
        {
            throw new NotImplementedException();
        }
    }
}
