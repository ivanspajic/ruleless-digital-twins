using Logic.Models.MapekModels;
using Logic.Models.OntologicalModels;
using Logic.ValueHandlerInterfaces;
using System.Globalization;

namespace SensorActuatorImplementations.ValueHandlers
{
    // Example int Sensor value handler implementation.
    public class ExampleIntValueHandler : IValueHandler
    {
        // When calculating possible reconfiguration values for ConfigurableParameters, some parameters may need specific logic to do so. For example,
        // it may be inaccurate to simply take the min-max value range and divide it by the simulation granularity in a completely linear way. For this
        // reason, the user may register custom logic delegates and map them to specific ConfigurableParameter names.
        private static readonly Dictionary<string, Func<int, int, int, int, IEnumerable<object>>> _configurableParameterGranularityMap = new() { };

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
            IEnumerable<object> possibleValues;

            if (currentValue is not int)
            {
                currentValue = int.Parse(currentValue.ToString()!, CultureInfo.InvariantCulture);
            }

            if (minimumValue is not int)
            {
                minimumValue = int.Parse(minimumValue.ToString()!, CultureInfo.InvariantCulture);
            }

            if (maximumValue is not int)
            {
                maximumValue = int.Parse(maximumValue.ToString()!, CultureInfo.InvariantCulture);
            }

            var currentValueInt = (int)currentValue;
            var minimumValueInt = (int)minimumValue;
            var maximumValueInt = (int)maximumValue;

            if (_configurableParameterGranularityMap.TryGetValue(configurableParameterName, out Func<int, int, int, int, IEnumerable<object>> configurableParameterLogic))
            {
                possibleValues = configurableParameterLogic(currentValueInt, minimumValueInt, maximumValueInt, simulationGranularity);
            }
            else
            {
                var possibleValueList = new List<object>();

                var valueRange = maximumValueInt - minimumValueInt;
                // This is a rough rounding in cases of integer values remainders from granularity values.
                var intervalSize = (int)Math.Floor((double)valueRange / simulationGranularity);

                for (var i = minimumValueInt; i < maximumValueInt; i += intervalSize)
                {
                    if ((effect == Effect.ValueIncrease && i > currentValueInt) || (effect == Effect.ValueDecrease && i < currentValueInt))
                    {
                        possibleValueList.Add(i);
                    }
                }

                possibleValues = possibleValueList;
            }

            return possibleValues;
        }

        public IEnumerable<AtomicConstraintExpression> GetUnsatisfiedConstraintsFromEvaluation(ConstraintExpression constraintExpression, object propertyValue)
        {
            throw new NotImplementedException();
        }
    }
}
