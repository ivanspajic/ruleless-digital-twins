using CsvHelper;
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

        // In case of more ways of combining constraint propositions of Conditions, this could be used to register new delegates.
        private static readonly Dictionary<ConstraintType, Func<bool, bool, bool>> _expressionCombinationDelegateMap = new()
        {
            { ConstraintType.And, EvaluateAnd },
            { ConstraintType.Or, EvaluateOr }
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

        // This can be removed. It's only being referenced by obsolete MapekAnalyze.
        public IEnumerable<object> GetPossibleValuesForActuationAction(Actuator actuator)
        {
            throw new NotImplementedException();
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

        public string GetValueAsCultureInvariantString(object value)
        {
            if (value is not double)
            {
                value = double.Parse(value.ToString()!, CultureInfo.InvariantCulture);
            }
            return ((double)value).ToString(CultureInfo.InvariantCulture);
        }

        public object GetQuantizedValue(object value, double fuzziness) {
            if (value is not double) {
                value = double.Parse(value.ToString()!, CultureInfo.InvariantCulture);
            }

            var factor = (double)value / fuzziness;
            var remainder = (double)value % fuzziness;
            var halfFuzziness = fuzziness / 2;

            if (remainder > halfFuzziness) {
                return Math.Ceiling(factor) * fuzziness;
            } else {
                return Math.Floor(factor) * fuzziness;
            }
        }

        private static bool EvaluateGreaterThan(double sensorValue, double conditionValue)
        {
            return sensorValue > conditionValue;
        }

        private static bool EvaluateGreaterThanOrEqualTo(double sensorValue, double conditionValue)
        {
            return sensorValue >= conditionValue;
        }

        private static bool EvaluateLessThan(double sensorValue, double conditionValue)
        {
            return sensorValue < conditionValue;
        }

        private static bool EvaluateLessThanOrEqualTo(double sensorValue, double conditionValue)
        {
            return sensorValue <= conditionValue;
        }

        private static bool EvaluateAnd(bool left, bool right)
        {
            return left && right;
        }

        private static bool EvaluateOr(bool left, bool right)
        {
            return left || right;
        }
    }
}
