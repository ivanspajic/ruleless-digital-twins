using VDS.RDF.Query;

namespace Logic.SensorValueHandlers
{
    internal class SensorDoubleValueHandler : ISensorValueHandler
    {
        public Tuple<object, object> FindObservablePropertyValueRange(SparqlResultSet queryResult,
            string queryVariableName,
            IDictionary<string, object> measuredPropertyMap)
        {
            var lowestValue = double.MaxValue;
            var highestValue = double.MinValue;

            foreach (var result in queryResult.Results)
            {
                var propertyName = result[queryVariableName].ToString();
                var propertyValue = (double)measuredPropertyMap[propertyName];

                if (propertyValue < lowestValue)
                    lowestValue = propertyValue;

                if (propertyValue > highestValue)
                    highestValue = propertyValue;
            }

            return new Tuple<object, object>(lowestValue, highestValue);
        }
    }
}
