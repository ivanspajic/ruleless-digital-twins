using Logic.SensorValueHandlers;
using Models;
using System.Globalization;

namespace SensorActuatorImplementations.ValueHandlers
{
    public class SensorDoubleValueHandler : ISensorValueHandler
    {
        // In case of new ConstraintOperators being supported, this could be used to register new delegates.
        private static readonly Dictionary<ConstraintOperator, Func<double, double, bool>> _expressionDelegateMap = new()
        {
            { ConstraintOperator.EqualTo, EvaluateEqualTo },
            { ConstraintOperator.GreaterThan, EvaluateGreaterThan },
            { ConstraintOperator.GreaterThanOrEqualTo, EvaluateGreaterThanOrEqualTo },
            { ConstraintOperator.LessThan, EvaluateLessThan },
            { ConstraintOperator.LessThanOrEqualTo, EvaluateLessThanOrEqualTo }
        };

        // In case of new Effects being added, this could be used to register new delegates.
        private static readonly Dictionary<Effect, Func<double, double, double>> _amountChangeDelegateMap = new()
        {
            { Effect.ValueIncrease, IncreaseValueByAmount },
            { Effect.ValueDecrease, DecreaseValueByAmount }
        };

        public bool EvaluateConstraint(object sensorValue, Tuple<ConstraintOperator, string> constraint)
        {
            if (_expressionDelegateMap.TryGetValue(constraint.Item1, out Func<double, double, bool>? evaluator))
            {
                // The constraint value comes directly from the graph as a string.
                return evaluator((double)sensorValue, double.Parse(constraint.Item2, CultureInfo.InvariantCulture));
            }

            throw new Exception($"Unsupported constraint operator {constraint.Item1}.");
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
                // In all other cases, since the ConfigurableParameter's value is cached, it will be a double.
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
