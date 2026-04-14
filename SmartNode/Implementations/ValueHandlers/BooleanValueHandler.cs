using Femyou;
using Logic.Models.MapekModels;
using Logic.Models.OntologicalModels;
using Logic.ValueHandlerInterfaces;
using System.Globalization;

namespace Implementations.ValueHandlers
{
    public class BooleanValueHandler : IValueHandler
    {
    
        public object GetObservablePropertyValueFromMeasuredPropertyValues(params object[] measuredPropertyValues){
            // This is just an example implementation for this data type.
            var result = true;

            foreach (var measuredPropertyValue in measuredPropertyValues) {
                result = result && (bool)measuredPropertyValue;
            }

            return result;
        }

        public IEnumerable<object> GetPossibleValuesForActuationAction(Actuator actuator) {
            throw new ArgumentException($"Actuator {actuator.Name} has no implementation for possible values.");
        }

        public IEnumerable<object> GetPossibleValuesForReconfigurationAction(ConfigurableParameter configurableParameter, Effect effect) {
            throw new ArgumentException($"ConfigurableParameter {configurableParameter} has no implementation for possible values.");
        }

        public object GetInitialValueForConfigurableParameter(string configurableParameter) {
            throw new ArgumentException($"ConfigurableParameter {configurableParameter} has no added initial value.");
        }

        public IEnumerable<AtomicConstraintExpression> GetUnsatisfiedConstraintsFromEvaluation(ConstraintExpression constraintExpression, object propertyValue) {
            throw new NotImplementedException();
        }

        public object GetValueFromSimulationParameter(IInstance fmuInstance, IVariable parameter) {
            return fmuInstance.ReadBoolean(parameter).ToArray()[0];
        }

        public void WriteValueToSimulationParameter(IInstance fmuInstance, IVariable parameter, object value) {
            if (value is not bool) {
                value = bool.Parse(value.ToString()!);
            }

            fmuInstance.WriteBoolean((parameter, (bool)value));
        }

        public bool IsGreaterThanOrEqualTo(object comparingValue, object targetValue) {
            throw new NotImplementedException();
        }

        public bool IsLessThanOrEqualTo(object comparingValue, object targetValue) {
            throw new NotImplementedException();
        }

        public string GetValueAsCultureInvariantString(object value) {
            if (value is not bool) {
                value = bool.Parse(value.ToString()!);
            }
            return ((bool)value).ToString(CultureInfo.InvariantCulture);
        }

        public int IncreaseComp(object comparingValue, object targetValue) {
            throw new NotImplementedException();
        }
    }
}
