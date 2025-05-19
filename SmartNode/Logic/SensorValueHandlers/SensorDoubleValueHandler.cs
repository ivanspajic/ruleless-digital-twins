using Models;
using VDS.RDF.Query;

namespace Logic.SensorValueHandlers
{
    internal class SensorDoubleValueHandler : ISensorValueHandler
    {
        private static readonly Dictionary<ConstraintOperator, Func<double, double, bool>> _expressionDelegateMap = new()
        {
            { ConstraintOperator.EqualTo, EvaluateEquals },
            { ConstraintOperator.GreaterThan, EvaluateGreaterThan },
            { ConstraintOperator.GreaterThanOrEqualTo, EvaluateGreaterThanOrEqualTo },
            { ConstraintOperator.LessThan, EvaluateLessThan },
            { ConstraintOperator.LessThanOrEqualTo, EvaluateLessThanOrEqualTo }
        };

        public Tuple<object, object> FindObservablePropertyValueRange(SparqlResultSet queryResult,
            string queryVariableName,
            IDictionary<string, InputOutput> inputOutputs)
        {
            var lowestValue = double.MaxValue;
            var highestValue = double.MinValue;

            foreach (var result in queryResult.Results)
            {
                var propertyName = result[queryVariableName].ToString();
                var propertyValue = (double)inputOutputs[propertyName].Value;

                if (propertyValue < lowestValue)
                    lowestValue = propertyValue;

                if (propertyValue > highestValue)
                    highestValue = propertyValue;
            }

            return new Tuple<object, object>(lowestValue, highestValue);
        }

        public bool EvaluateConstraint(object sensorValue, Tuple<ConstraintOperator, object> constraint)
        {
            if (_expressionDelegateMap.TryGetValue(constraint.Item1, out Func<double, double, bool>? evaluator))
                return evaluator((double)constraint.Item2, (double)sensorValue);

            throw new Exception("Unsupported constraint operator.");
        }

        private static bool EvaluateEquals(double sensorValue, double optimalConditionValue)
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
    }
}
