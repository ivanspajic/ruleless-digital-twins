using Logic.Models.OntologicalModels;
using Logic.ValueHandlerInterfaces;
using System.Globalization;
using System.Linq.Expressions;
using VDS.RDF.Writing.Formatting;

namespace SensorActuatorImplementations.ValueHandlers
{
    // Compares doubles in a simplistic way, but it should do for most intents and purposes.
    public class ExampleDoubleValueHandler : IValueHandler
    {
        // In case of new ExpressionTypes being supported, this could be used to register new delegates.
        private static readonly Dictionary<ExpressionType, Func<double, double, bool>> _expressionDelegateMap = new()
        {
            { ExpressionType.Equal, EvaluateEqualTo },
            { ExpressionType.GreaterThan, EvaluateGreaterThan },
            { ExpressionType.GreaterThanOrEqual, EvaluateGreaterThanOrEqualTo },
            { ExpressionType.LessThan, EvaluateLessThan },
            { ExpressionType.LessThanOrEqual, EvaluateLessThanOrEqualTo },
        };

        // In case of more ways of combining constraint propositions of OptimalConditions, this could be used to register new
        // delegates.
        private static readonly Dictionary<ExpressionType, Func<bool, bool, bool>> _expressionCombinationDelegateMap = new()
        {
            { ExpressionType.And, EvaluateAnd },
            { ExpressionType.Or, EvaluateOr }
        };

        // In case of new Effects being added, this could be used to register new delegates.
        private static readonly Dictionary<Effect, Func<double, double, double>> _amountChangeDelegateMap = new()
        {
            { Effect.ValueIncrease, IncreaseValueByAmount },
            { Effect.ValueDecrease, DecreaseValueByAmount }
        };

        public IEnumerable<BinaryExpression> GetUnsatisfiedConstraintsFromEvaluation(BinaryExpression constraintExpression)
        {
            var unsatisfiedConstraints = new List<BinaryExpression>();

            if (_expressionDelegateMap.TryGetValue(constraintExpression.NodeType, out Func<double, double, bool> valueComparisonEvaluator))
            {
                // In case of finding the node type in the expression delegate map, we know it must be a binary expression with constant values
                // to be compared.
                var left = ((ConstantExpression)constraintExpression.Left).Value!;
                var right = ((ConstantExpression)constraintExpression.Right).Value!;

                if (left is not double)
                {
                    left = double.Parse(left.ToString()!, CultureInfo.InvariantCulture);
                }

                if (right is not double)
                {
                    right = double.Parse(right.ToString()!, CultureInfo.InvariantCulture);
                }

                // Evaluate the comparison and add the atomic 
                var evaluation = valueComparisonEvaluator((double)left, (double)right);

                if (!evaluation)
                {
                    unsatisfiedConstraints.Add(constraintExpression);
                }
            }
            else if (_expressionCombinationDelegateMap.TryGetValue(constraintExpression.NodeType, out Func<bool, bool, bool> constraintCombinationEvaluator))
            {
                // In case of finding the node type in the expression combination delegate map, we know it must be a binary expression with more
                // sub-expression either containing more combinations or value comparisons.
                var leftUnsatisfiedConstraints = GetUnsatisfiedConstraintsFromEvaluation((BinaryExpression)constraintExpression.Left);
                var rightUnsatisfiedConstraints = GetUnsatisfiedConstraintsFromEvaluation((BinaryExpression)constraintExpression.Right);

                var evaluation = constraintCombinationEvaluator(leftUnsatisfiedConstraints.Any(), rightUnsatisfiedConstraints.Any());

                if (!evaluation)
                {
                    unsatisfiedConstraints.AddRange(leftUnsatisfiedConstraints);
                    unsatisfiedConstraints.AddRange(rightUnsatisfiedConstraints);
                }
            }
            else
            {
                throw new Exception($"Unsupported expression node type: {constraintExpression.NodeType}");
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

        public object ChangeValueByAmount(object value, object amountToChangeBy, Effect typeOfChange)
        {
            if (_amountChangeDelegateMap.TryGetValue(typeOfChange, out Func<double, double, double>? valueUpdater))
            {
                // Check if the value is not already a double to parse it before proceeding. This ensures that we parse
                // during the initial run when the value is given as a string directly from the instance model graph.
                // In all other cases, since the ConfigurableParameter's value is cached, it will be a double (object).
                if (value is not double)
                {
                    value = double.Parse(value.ToString()!, CultureInfo.InvariantCulture);
                }
                
                return valueUpdater((double)value, (double)amountToChangeBy);
            }

            throw new Exception($"Unsupported Effect {typeOfChange}.");
        }

        private static bool EvaluateEqualTo(double sensorValue, double optimalConditionValue)
        {
            return sensorValue == optimalConditionValue;
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
