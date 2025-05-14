using VDS.RDF.Query;

namespace Logic.Utilities
{
    internal interface ISensorValueHandler
    {
        public Tuple<object, object> FindObservablePropertyValueRange(SparqlResultSet queryResult,
            string queryVariableName,
            IDictionary<string, object> measuredPropertyMap);
    }
}
