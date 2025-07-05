using Logic.Models.OntologicalModels;
using System.Linq.Expressions;

namespace Logic.ValueHandlerInterfaces
{
    public interface IValueHandler
    {
        public bool EvaluateConstraints(BinaryExpression constraintExpression);

        public object GetObservablePropertyValueFromMeasuredPropertyValues(params object[] measuredPropertyValues);

        public object ChangeValueByAmount(object value, object amountToChangeBy, Effect typeOfChange);
    }
}
