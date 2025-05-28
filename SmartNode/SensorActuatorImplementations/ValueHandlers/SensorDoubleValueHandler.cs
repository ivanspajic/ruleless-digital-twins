using Logic.SensorValueHandlers;
using Models;
using System.Globalization;

namespace SensorActuatorImplementations.ValueHandlers
{
    public class SensorDoubleValueHandler : ISensorValueHandler
    {
        private static readonly Dictionary<ConstraintOperator, Func<double, double, bool>> _expressionDelegateMap = new()
        {
            { ConstraintOperator.EqualTo, EvaluateEqualTo },
            { ConstraintOperator.NotEqualTo, EvaluateNotEqualTo },
            { ConstraintOperator.GreaterThan, EvaluateGreaterThan },
            { ConstraintOperator.GreaterThanOrEqualTo, EvaluateGreaterThanOrEqualTo },
            { ConstraintOperator.LessThan, EvaluateLessThan },
            { ConstraintOperator.LessThanOrEqualTo, EvaluateLessThanOrEqualTo }
        };

        public bool EvaluateConstraint(object sensorValue, Tuple<ConstraintOperator, string> constraint)
        {
            if (_expressionDelegateMap.TryGetValue(constraint.Item1, out Func<double, double, bool>? evaluator))
                // The constraint value comes directly from the graph as a string.
                return evaluator(double.Parse(constraint.Item2, CultureInfo.InvariantCulture), (double)sensorValue);

            throw new Exception("Unsupported constraint operator.");
        }

        public object GetObservablePropertyValueFromMeasuredPropertyValues(params object[] measuredPropertyValues)
        {
            var measuredPropertyDoubleValues = new double[measuredPropertyValues.Length];

            for (var i = 0; i < measuredPropertyValues.Length; i++)
            {
                measuredPropertyDoubleValues[i] = (double)measuredPropertyValues[i];
            }

            // This logic is currently hard-coded to calculate the average of measured Properties and use that as
            // the ObservableProperty's value. This could however be outsourced to a user-defined logic delegate
            // containing whatever calculation for determining ObservableProperty values.
            return measuredPropertyDoubleValues.Sum() / measuredPropertyDoubleValues.Length;
        }

        private static bool EvaluateEqualTo(double sensorValue, double optimalConditionValue)
        {
            return sensorValue == optimalConditionValue;
        }

        private static bool EvaluateNotEqualTo(double sensorValue, double optimalConditionValue)
        {
            return sensorValue != optimalConditionValue;
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
    }
}
