using Femyou;
using Logic.Models.MapekModels;
using Logic.Models.OntologicalModels;
using Logic.ValueHandlerInterfaces;
using System.Globalization;

namespace Implementations.ValueHandlers
{
    // Example int Sensor value handler implementation.
    public class IntValueHandler : IValueHandler
    {
        // When calculating possible reconfiguration values for ConfigurableParameters, some parameters may need specific logic to do so. For example,
        // it may be inaccurate to simply take the min-max value range and divide it by the simulation granularity in a completely linear way. For this
        // reason, the user may register custom logic delegates and map them to specific ConfigurableParameter names.
        private static readonly Dictionary<string, Func<object, Effect, IEnumerable<object>>> _configurableParameterPossibleValuesMap = new()
        {
            { "http://www.semanticweb.org/ispa/ontologies/2025/instance-model-2/BucketSize", GetPossibleBucketSizeValues }
        };

        private static readonly Dictionary<string, object> _initialConfigurableParameterValues = new()
        {
            { "http://www.semanticweb.org/ispa/ontologies/2025/instance-model-2/BucketSize", 7 }
        };

        private static readonly Dictionary<string, IEnumerable<object>> _actuatorStatePossibleValues = new()
        {
            { "http://www.semanticweb.org/ivans/ontologies/2025/instance-model-1#AirConditioningUnit", new List<object>
                {
                    0,
                    1,
                    2,
                    3
                }
            },
            { "http://www.semanticweb.org/ivans/ontologies/2025/instance-model-1#Dehumidifier", new List<object>
                {
                    0,
                    1
                }
            }
        };

        public object GetObservablePropertyValueFromMeasuredPropertyValues(params object[] measuredPropertyValues)
        {
            throw new NotImplementedException();
        }

        public IEnumerable<object> GetPossibleValuesForActuationAction(Actuator actuator)
        {
            if (_actuatorStatePossibleValues.TryGetValue(actuator.Name, out IEnumerable<object>? possibleValues))
            {
                return possibleValues;
            }
            else
            {
                throw new ArgumentException($"Actuator {actuator.Name} has no implementation for possible values.");
            }
        }

        public IEnumerable<object> GetPossibleValuesForReconfigurationAction(ConfigurableParameter configurableParameter, Effect effect)
        {
            if (_configurableParameterPossibleValuesMap.TryGetValue(configurableParameter.Name, out Func<object, Effect, IEnumerable<object>>? configurableParameterLogic))
            {
                return configurableParameterLogic(configurableParameter.Value, effect);
            }
            else
            {
                throw new ArgumentException($"ConfigurableParameter {configurableParameter} has no implementation for possible values.");
            }
        }

        public object GetInitialValueForConfigurableParameter(string configurableParameter)
        {
            if (_initialConfigurableParameterValues.TryGetValue(configurableParameter, out object? initialValue))
            {
                return initialValue;
            }
            else
            {
                throw new ArgumentException($"ConfigurableParameter {configurableParameter} has no added initial value.");
            }
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

        private static IEnumerable<object> GetPossibleBucketSizeValues(object currentValue, Effect effect)
        {
            var rangeGranularity = 10;

            if (currentValue is not int)
            {
                currentValue = int.Parse(currentValue.ToString()!, CultureInfo.InvariantCulture);
            }

            var currentValueInt = (int)currentValue;

            var minimumValue = 3;
            var maximumValue = 20;

            var possibleValues = new List<object>
            {
                currentValueInt
            };

            var valueRange = maximumValue - minimumValue;
            // This is a rough rounding in cases of integer values remainders from granularity values.
            var intervalSize = (int)Math.Floor((double)valueRange / (rangeGranularity - 1));

            for (var i = minimumValue; i < maximumValue; i += intervalSize)
            {
                if ((effect == Effect.ValueIncrease && i > currentValueInt) || (effect == Effect.ValueDecrease && i < currentValueInt))
                {
                    possibleValues.Add(i);
                }
            }

            return possibleValues;
        }

        public int IncreaseComp(object comparingValue, object targetValue)
        {
            throw new NotImplementedException();
        }
    }
}
