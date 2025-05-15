using Models;
using VDS.RDF.Query;

namespace Logic.SensorValueHandlers
{
    internal class SensorIntValueHandler : ISensorValueHandler
    {
        public Tuple<object, object> FindObservablePropertyValueRange(SparqlResultSet queryResult,
            string queryVariableName,
            IDictionary<string, InputOutput> inputOutputs)
        {
            var lowestValue = int.MaxValue;
            var highestValue = int.MinValue;

            foreach (var result in queryResult.Results)
            {
                var propertyName = result[queryVariableName].ToString();
                var propertyValue = (int)inputOutputs[propertyName].Value;

                if (propertyValue < lowestValue)
                    lowestValue = propertyValue;

                if (propertyValue > highestValue)
                    highestValue = propertyValue;
            }

            return new Tuple<object, object>(lowestValue, highestValue);
        }
    }
}
