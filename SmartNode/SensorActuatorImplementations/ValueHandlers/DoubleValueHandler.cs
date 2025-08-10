using Femyou;
using Logic.Models.MapekModels;
using Logic.Models.OntologicalModels;
using Logic.ValueHandlerInterfaces;
using System.Globalization;

namespace SensorActuatorImplementations.ValueHandlers
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
        private static readonly Dictionary<string, Func<double, double, double, int, IEnumerable<object>>> _configurableParameterGranularityMap = new() { };

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

        public IEnumerable<object> GetPossibleValuesForReconfigurationAction(ConfigurableParameter configurableParameter, int simulationGranularity, Effect effect)
        {
            IEnumerable<object> possibleValues;

            var currentValue = configurableParameter.Value;
            var minimumValue = configurableParameter.LowerLimitValue;
            var maximumValue = configurableParameter.UpperLimitValue;

            if (currentValue is not double)
            {
                currentValue = double.Parse(currentValue.ToString()!, CultureInfo.InvariantCulture);
            }

            if (minimumValue is not double)
            {
                minimumValue = double.Parse(minimumValue.ToString()!, CultureInfo.InvariantCulture);
            }

            if (maximumValue is not double)
            {
                maximumValue = double.Parse(maximumValue.ToString()!, CultureInfo.InvariantCulture);
            }

            var currentValueDouble = (double)currentValue;
            var minimumValueDouble = (double)minimumValue;
            var maximumValueDouble = (double)maximumValue;

            if (_configurableParameterGranularityMap.TryGetValue(configurableParameter.Name, out Func<double, double, double, int, IEnumerable<object>>? configurableParameterLogic))
            {
                possibleValues = configurableParameterLogic(currentValueDouble, minimumValueDouble, maximumValueDouble, simulationGranularity);
            }
            else
            {
                var possibleValueList = new List<object>();

                var valueRange = maximumValueDouble - minimumValueDouble;
                var intervalSize = valueRange / simulationGranularity;

                for (var i = minimumValueDouble; i < maximumValueDouble; i += intervalSize)
                {
                    if ((effect == Effect.ValueIncrease && i > currentValueDouble) || (effect == Effect.ValueDecrease && i < currentValueDouble))
                    {
                        possibleValueList.Add(i);
                    }
                }

                possibleValues = possibleValueList;
            }

            return possibleValues;
        }

        public bool IsGreaterThan(object comparingValue, object targetValue)
        {
            if (comparingValue is not double)
            {
                comparingValue = double.Parse(comparingValue.ToString()!, CultureInfo.InvariantCulture);
            }

            if (targetValue is not double)
            {
                targetValue = double.Parse(targetValue.ToString()!, CultureInfo.InvariantCulture);
            }

            return EvaluateGreaterThan((double)comparingValue, (double)targetValue);
        }

        public bool IsLessThan(object comparingValue, object targetValue)
        {
            if (comparingValue is not double)
            {
                comparingValue = double.Parse(comparingValue.ToString()!, CultureInfo.InvariantCulture);
            }

            if (targetValue is not double)
            {
                targetValue = double.Parse(targetValue.ToString()!, CultureInfo.InvariantCulture);
            }

            return EvaluateLessThan((double)comparingValue, (double)targetValue);
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
    }
}
