using VDS.RDF;
using VDS.RDF.Query;

namespace Logic.Mapek
{
    internal static class MapekUtilities
    {
        private const string DtPrefix = "meta";
        private const string DtUri = "http://www.semanticweb.org/ivans/ontologies/2025/dt-code-generation/";
        private const string SosaPrefix = "sosa";
        private const string SosaUri = "http://www.w3.org/ns/sosa/";
        private const string SsnPrefix = "ssn";
        private const string SsnUri = "http://www.w3.org/ns/ssn/";
        private const string RdfPrefix = "rdf";
        private const string RdfUri = "http://www.w3.org/1999/02/22-rdf-syntax-ns#";
        private const string OwlPrefix = "owl";
        private const string OwlUri = "http://www.w3.org/2002/07/owl#";
        private const string XsdPrefix = "xsd";
        private const string XsdUri = "http://www.w3.org/2001/XMLSchema#";

        public static SparqlParameterizedString GetParameterizedStringQuery()
        {
            var query = new SparqlParameterizedString();

            // Register the relevant prefixes for the queries to come.
            query.Namespaces.AddNamespace(DtPrefix, new Uri(DtUri));
            query.Namespaces.AddNamespace(SosaPrefix, new Uri(SosaUri));
            query.Namespaces.AddNamespace(SsnPrefix, new Uri(SsnUri));
            query.Namespaces.AddNamespace(RdfPrefix, new Uri(RdfUri));
            query.Namespaces.AddNamespace(OwlPrefix, new Uri(OwlUri));
            query.Namespaces.AddNamespace(XsdPrefix, new Uri(XsdUri));

            return query;
        }

        public static string GetPropertyValueType(IGraph instanceModel, INode propertyNode)
        {
            var query = GetParameterizedStringQuery();

            query.CommandText = @"SELECT ?valueType WHERE {
                @property rdf:type ?bNode .
                ?bNode owl:onProperty meta:hasValue .
                ?bNode owl:onDataRange ?valueType . }";

            return GetPropertyValueType(query, instanceModel, "property", propertyNode);
        }

        public static string GetObservablePropertyValueType(IGraph instanceModel, INode propertyNode)
        {
            var query = GetParameterizedStringQuery();

            query.CommandText = @"SELECT ?valueType WHERE {
                @property rdf:type sosa:ObservableProperty .
                @property rdf:type ?bNode .
                ?bNode owl:onProperty meta:hasUpperLimitValue .
                ?bNode owl:onDataRange ?valueType . }";

            return GetPropertyValueType(query, instanceModel, "property", propertyNode);
        }

        private static string GetPropertyValueType(SparqlParameterizedString query, IGraph instanceModel, string parameterName, INode propertyNode)
        {
            query.SetParameter(parameterName, propertyNode);

            var propertyTypeQueryResult = (SparqlResultSet)instanceModel.ExecuteQuery(query);

            if (propertyTypeQueryResult.IsEmpty)
            {
                throw new Exception("The property " + propertyNode.ToString() + " was found without a value type.");
            }

            var propertyValueType = propertyTypeQueryResult.Results[0]["valueType"].ToString();
            return propertyValueType.Split('#')[1];
        }
    }
}
