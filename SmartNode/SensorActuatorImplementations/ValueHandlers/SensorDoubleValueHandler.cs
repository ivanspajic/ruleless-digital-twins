using Logic.SensorValueHandlers;
using Models;

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
            // The constraint value comes directly from the graph as a string.
            if (_expressionDelegateMap.TryGetValue(constraint.Item1, out Func<double, double, bool>? evaluator))
                return evaluator(double.Parse(constraint.Item2), (double)sensorValue);

            throw new Exception("Unsupported constraint operator.");
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
