using Femyou;
using Logic.Models.MapekModels;
using Logic.ValueHandlerInterfaces;
using System.Globalization;

namespace Implementations.ValueHandlers
{
    public class StringValueHandler : IValueHandler
    {
    
        public object GetObservablePropertyValueFromMeasuredPropertyValues(params object[] measuredPropertyValues){
            throw new NotImplementedException();
        }

        public object GetInitialValueForConfigurableParameter(string configurableParameter) {
            throw new ArgumentException($"ConfigurableParameter {configurableParameter} has no added initial value.");
        }

        public IEnumerable<AtomicConstraintExpression> GetUnsatisfiedConstraintsFromEvaluation(ConstraintExpression constraintExpression, object propertyValue) {
            throw new NotImplementedException();
        }

        public object GetValueFromSimulationParameter(IInstance fmuInstance, IVariable parameter) {
            return fmuInstance.ReadString(parameter).ToArray()[0];
        }

        public void WriteValueToSimulationParameter(IInstance fmuInstance, IVariable parameter, object value) {
            if (value is not string) {
                value = value.ToString()!;
            }

            fmuInstance.WriteString((parameter, (string)value));
        }

        public bool IsGreaterThanOrEqualTo(object comparingValue, object targetValue) {
            throw new NotImplementedException();
        }

        public bool IsLessThanOrEqualTo(object comparingValue, object targetValue) {
            throw new NotImplementedException();
        }

        public string GetValueAsCultureInvariantString(object value) {
            if (value is not string) {
                value = value.ToString()!;
            }
            return ((string)value).ToString(CultureInfo.InvariantCulture);
        }

        public int IncreaseComp(object comparingValue, object targetValue) {
            throw new NotImplementedException();
        }

        public object GetQuantizedValue(object value, double fuzziness) {
            throw new NotImplementedException();
        }
    }
}
