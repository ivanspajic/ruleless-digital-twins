using Models;
using VDS.RDF.Query;

namespace Logic.SensorValueHandlers
{
    // Example int Sensor value handler implementation.
    public class SensorIntValueHandler : ISensorValueHandler
    {
        public Tuple<object, object> FindObservablePropertyValueRange(SparqlResultSet queryResult,
            string queryVariableName,
            IDictionary<string, InputOutput> inputOutputs)
        {
            throw new NotImplementedException();
        }

        public bool EvaluateConstraint(object sensorValue, Tuple<ConstraintOperator, string> constraint)
        {
            throw new NotImplementedException();
        }
    }
}
