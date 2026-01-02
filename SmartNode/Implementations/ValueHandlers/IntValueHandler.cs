using Femyou;
using Logic.Models.MapekModels;
using Logic.ValueHandlerInterfaces;
using System.Globalization;

namespace Implementations.ValueHandlers
{
    public class IntValueHandler : IValueHandler
    {
        public object GetObservablePropertyValueFromMeasuredPropertyValues(params object[] measuredPropertyValues)
        {
            throw new NotImplementedException();
        }

        public IEnumerable<AtomicConstraintExpression> GetUnsatisfiedConstraintsFromEvaluation(ConstraintExpression constraintExpression, object propertyValue)
        {
            throw new NotImplementedException();
        }

        public object GetValueFromSimulationParameter(IInstance fmuInstance, IVariable parameter)
        {
            return fmuInstance.ReadInteger(parameter).ToArray()[0];
        }

        public void WriteValueToSimulationParameter(IInstance fmuInstance, IVariable parameter, object value)
        {
            if (value is not int)
            {
                value = int.Parse(value.ToString()!, CultureInfo.InvariantCulture);
            }

            fmuInstance.WriteInteger((parameter, (int)value));
        }

        public bool IsGreaterThanOrEqualTo(object comparingValue, object targetValue)
        {
            throw new NotImplementedException();
        }

        public bool IsLessThanOrEqualTo(object comparingValue, object targetValue)
        {
            throw new NotImplementedException();
        }

        public string GetValueAsCultureInvariantString(object value) {
            if (value is not int) {
                value = int.Parse(value.ToString()!, CultureInfo.InvariantCulture);
            }
            return ((int)value).ToString(CultureInfo.InvariantCulture);
        }

        public int IncreaseComp(object comparingValue, object targetValue) {
            if (comparingValue is not int) {
                comparingValue = int.Parse(comparingValue.ToString()!, CultureInfo.InvariantCulture);
            }

            if (targetValue is not int) {
                targetValue = int.Parse(targetValue.ToString()!, CultureInfo.InvariantCulture);
            }

            if ((int)comparingValue > (int)targetValue) {
                return 1;
            } else if ((int)comparingValue < (int)targetValue) {
                return -1;
            } else {
                return 0;
            }
        }

        public object GetQuantizedValue(object value, double fuzziness) {
            if (value is not int) {
                value = int.Parse(value.ToString()!, CultureInfo.InvariantCulture);
            }

            var factor = (int)value / fuzziness;
            var remainder = (int)value % fuzziness;
            var halfFuzziness = fuzziness / 2;

            if (remainder > halfFuzziness) {
                return Math.Ceiling(factor) * fuzziness;
            } else {
                return Math.Floor(factor) * fuzziness;
            }
        }
    }
}
