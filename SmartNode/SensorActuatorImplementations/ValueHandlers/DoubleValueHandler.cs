using Femyou;
using Logic.Models.MapekModels;
using Logic.Models.OntologicalModels;
using Logic.ValueHandlerInterfaces;
using System.Globalization;

namespace Implementations.ValueHandlers
{
    public class DoubleValueHandler : IValueHandler
    {
        // In case of new ExpressionTypes being supported, this could be used to register new delegates.
        private static readonly Dictionary<ConstraintType, Func<double, double, bool>> _expressionDelegateMap = new()
        {
            { ConstraintType.GreaterThan, EvaluateGreaterThan },
            { ConstraintType.GreaterThanOrEqualTo, EvaluateGreaterThanOrEqualTo },
            { ConstraintType.LessThan, EvaluateLessThan },
            { ConstraintType.LessThanOrEqualTo, EvaluateLessThanOrEqualTo },
        };

        // In case of more ways of combining constraint propositions of OptimalConditions, this could be used to register new
        // delegates.
        private static readonly Dictionary<ConstraintType, Func<bool, bool, bool>> _expressionCombinationDelegateMap = new()
        {
            { ConstraintType.And, EvaluateAnd },
            { ConstraintType.Or, EvaluateOr }
        };

        // In case of new Effects being added, this could be used to register new delegates.
        private static readonly Dictionary<Effect, Func<double, double, double>> _amountChangeDelegateMap = new()
        {
            { Effect.ValueIncrease, IncreaseValueByAmount },
            { Effect.ValueDecrease, DecreaseValueByAmount }
        };

        // When calculating possible reconfiguration values for ConfigurableParameters, some parameters may need specific logic to do so. For example,
        // it may be inaccurate to simply take the min-max value range and divide it by the simulation granularity in a completely linear way. For this
        // reason, the user may register custom logic delegates and map them to specific ConfigurableParameter names.
        private static readonly Dictionary<string, Func<object, Effect, IEnumerable<object>>> _configurableParameterPossibleValuesMap = new()
        {
            { "http://www.semanticweb.org/ispa/ontologies/2025/instance-model-2/Epsilon", GetPossibleEpsilonValues }
        };

        private static readonly Dictionary<string, object> _initialConfigurableParameterValues = new()
        {
            { "http://www.semanticweb.org/ispa/ontologies/2025/instance-model-2/Epsilon", 2.3 }
        };

        public IEnumerable<AtomicConstraintExpression> GetUnsatisfiedConstraintsFromEvaluation(ConstraintExpression constraintExpression, object propertyValue)
        {
            var unsatisfiedConstraints = new List<AtomicConstraintExpression>();

            if (_expressionDelegateMap.TryGetValue(constraintExpression.ConstraintType, out Func<double, double, bool>? valueComparisonEvaluator))
            {
                // In case of finding the node type in the expression delegate map, we know it must be a binary expression with constant values
                // to be compared.
                var atomicConstraintExpression = (AtomicConstraintExpression)constraintExpression;
                var right = atomicConstraintExpression.Right;

                if (propertyValue is not double)
                {
                    propertyValue = double.Parse(propertyValue.ToString()!, CultureInfo.InvariantCulture);
                }

                if (right is not double)
                {
                    right = double.Parse(right.ToString()!, CultureInfo.InvariantCulture);
                }

                // Evaluate the comparison and add the atomic 
                var evaluation = valueComparisonEvaluator((double)propertyValue, (double)right);

                if (!evaluation)
                {
                    unsatisfiedConstraints.Add(atomicConstraintExpression);
                }
            }
            else if (_expressionCombinationDelegateMap.TryGetValue(constraintExpression.ConstraintType, out Func<bool, bool, bool>? constraintCombinationEvaluator))
            {
                // In case of finding the node type in the expression combination delegate map, we know it must be a binary expression with more
                // sub-expression either containing more combinations or value comparisons.
                var nestedConstraintExpression = (NestedConstraintExpression)constraintExpression;
                var leftUnsatisfiedConstraints = GetUnsatisfiedConstraintsFromEvaluation(nestedConstraintExpression.Left, propertyValue);
                var rightUnsatisfiedConstraints = GetUnsatisfiedConstraintsFromEvaluation(nestedConstraintExpression.Right, propertyValue);

                var evaluation = constraintCombinationEvaluator(!leftUnsatisfiedConstraints.Any(), !rightUnsatisfiedConstraints.Any());

                if (!evaluation)
                {
                    unsatisfiedConstraints.AddRange(leftUnsatisfiedConstraints);
                    unsatisfiedConstraints.AddRange(rightUnsatisfiedConstraints);
                }
            }
            else
            {
                throw new Exception($"Unsupported expression node type: {constraintExpression.ConstraintType}");
            }

            return unsatisfiedConstraints;
        }

        public object GetObservablePropertyValueFromMeasuredPropertyValues(params object[] measuredPropertyValues)
        {
            // This logic is currently hard-coded to calculate the average of measured Properties and use that as
            // the ObservableProperty's value. This could however be any user-defined logic containing whatever
            // calculation for determining ObservableProperty values.

            var measuredPropertyDoubleValues = new double[measuredPropertyValues.Length];

            for (var i = 0; i < measuredPropertyValues.Length; i++)
            {
                measuredPropertyDoubleValues[i] = (double)measuredPropertyValues[i];
            }

            return measuredPropertyDoubleValues.Sum() / measuredPropertyDoubleValues.Length;
        }

        public IEnumerable<object> GetPossibleValuesForActuationAction(Actuator actuator)
        {
            throw new NotImplementedException();
        }

        public IEnumerable<object> GetPossibleValuesForReconfigurationAction(ConfigurableParameter configurableParameter, Effect effect)
        {
            if (_configurableParameterPossibleValuesMap.TryGetValue(configurableParameter.Name, out Func<object, Effect, IEnumerable<object>>? configurableParameterLogic))
            {
                return configurableParameterLogic(configurableParameter.Value, effect);
            }
            else
            {
                throw new ArgumentException($"ConfigurableParameter {configurableParameter.Name} has no implementation for possible values.");
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

        public int IncreaseComp(object comparingValue, object targetValue)
        {
            if (comparingValue is not double)
            {
                comparingValue = double.Parse(comparingValue.ToString()!, CultureInfo.InvariantCulture);
            }

            if (targetValue is not double)
            {
                targetValue = double.Parse(targetValue.ToString()!, CultureInfo.InvariantCulture);
            }

            if ((double)comparingValue > (double)targetValue)
            {
                return 1;
            }
            else if ((double)comparingValue < (double)targetValue)
            {
                return -1;
            }
            else
            {
                return 0;
            }
        }

        public bool IsGreaterThanOrEqualTo(object comparingValue, object targetValue)
        {
            if (comparingValue is not double)
            {
                comparingValue = double.Parse(comparingValue.ToString()!, CultureInfo.InvariantCulture);
            }

            if (targetValue is not double)
            {
                targetValue = double.Parse(targetValue.ToString()!, CultureInfo.InvariantCulture);
            }

            return EvaluateGreaterThanOrEqualTo((double)comparingValue, (double)targetValue);
        }

        public bool IsLessThanOrEqualTo(object comparingValue, object targetValue)
        {
            if (comparingValue is not double)
            {
                comparingValue = double.Parse(comparingValue.ToString()!, CultureInfo.InvariantCulture);
            }

            if (targetValue is not double)
            {
                targetValue = double.Parse(targetValue.ToString()!, CultureInfo.InvariantCulture);
            }

            return EvaluateLessThanOrEqualTo((double)comparingValue, (double)targetValue);
        }

        public object GetValueFromSimulationParameter(IInstance fmuInstance, IVariable parameter)
        {
            return fmuInstance.ReadReal(parameter).ToArray()[0];
        }

        public void WriteValueToSimulationParameter(IInstance fmuInstance, IVariable parameter, object value)
        {
            if (value is not double)
            {
                value = double.Parse(value.ToString()!, CultureInfo.InvariantCulture);
            }

            fmuInstance.WriteReal((parameter, (double)value));
        }

        private static bool EvaluateGreaterThan(double sensorValue, double optimalConditionValue)
        {
            return sensorValue > optimalConditionValue;
        }

        private static bool EvaluateGreaterThanOrEqualTo(double sensorValue, double optimalConditionValue)
        {
            return sensorValue >= optimalConditionValue;
        }

        private static bool EvaluateLessThan(double sensorValue, double optimalConditionValue)
        {
            return sensorValue < optimalConditionValue;
        }

        private static bool EvaluateLessThanOrEqualTo(double sensorValue, double optimalConditionValue)
        {
            return sensorValue <= optimalConditionValue;
        }

        private static bool EvaluateAnd(bool left, bool right)
        {
            return left && right;
        }

        private static bool EvaluateOr(bool left, bool right)
        {
            return left || right;
        }

        private static double IncreaseValueByAmount(double value, double amountToIncreaseBy)
        {
            return value + amountToIncreaseBy;
        }

        private static double DecreaseValueByAmount(double value, double amountToDecreaseBy)
        {
            return value - amountToDecreaseBy;
        }

        private static IEnumerable<object> GetPossibleEpsilonValues(object currentValue, Effect effect)
        {
            var rangeGranularity = 10;

            if (currentValue is not double)
            {
                currentValue = double.Parse(currentValue.ToString()!, CultureInfo.InvariantCulture);
            }

            var currentValueDouble = (double)currentValue;

            var minimumValue = 0.0;
            var maximumValue = 12.0;

            var possibleValues = new List<object>
            {
                currentValueDouble
            };

            var valueRange = maximumValue - minimumValue;
            var intervalSize = valueRange / (rangeGranularity - 1);

            for (var i = minimumValue; i < maximumValue; i += intervalSize)
            {
                if ((effect == Effect.ValueIncrease && i > currentValueDouble) || (effect == Effect.ValueDecrease && i < currentValueDouble))
                {
                    possibleValues.Add(i);
                }
            }

            return possibleValues;
        }
    }
}
