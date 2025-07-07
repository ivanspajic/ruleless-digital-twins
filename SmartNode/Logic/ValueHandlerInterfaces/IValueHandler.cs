using Logic.Models.MapekModels;
using Logic.Models.OntologicalModels;

namespace Logic.ValueHandlerInterfaces
{
    public interface IValueHandler
    {
        public IEnumerable<AtomicConstraintExpression> GetUnsatisfiedConstraintsFromEvaluation(ConstraintExpression constraintExpression);

        public object GetObservablePropertyValueFromMeasuredPropertyValues(params object[] measuredPropertyValues);

        public object ChangeValueByAmount(object value, object amountToChangeBy, Effect typeOfChange);
    }
}
