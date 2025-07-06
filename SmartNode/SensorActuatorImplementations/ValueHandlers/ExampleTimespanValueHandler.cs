using Logic.Models.OntologicalModels;
using Logic.ValueHandlerInterfaces;
using System.Linq.Expressions;

namespace SensorActuatorImplementations.ValueHandlers
{
    public class ExampleTimespanValueHandler : IValueHandler
    {
        public object ChangeValueByAmount(object value, object amountToChangeBy, Effect typeOfChange)
        {
            throw new NotImplementedException();
        }

        public object GetObservablePropertyValueFromMeasuredPropertyValues(params object[] measuredPropertyValues)
        {
            throw new NotImplementedException();
        }

        public IEnumerable<BinaryExpression> GetUnsatisfiedConstraintsFromEvaluation(BinaryExpression constraintExpression)
        {
            throw new NotImplementedException();
        }
    }
}
