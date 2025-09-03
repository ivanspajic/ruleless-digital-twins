using Microsoft.Extensions.Logging;
using VDS.RDF;
using VDS.RDF.Query;

namespace Logic.Mapek
{
    internal static class MapekUtilities
    {
        public const string DtPrefix = "meta";
        public const string DtUri = "http://www.semanticweb.org/ivans/ontologies/2025/ruleless-digital-twins/";
        public const string SosaPrefix = "sosa";
        public const string SosaUri = "http://www.w3.org/ns/sosa/";
        public const string SsnPrefix = "ssn";
        public const string SsnUri = "http://www.w3.org/ns/ssn/";
        public const string RdfPrefix = "rdf";
        public const string RdfUri = "http://www.w3.org/1999/02/22-rdf-syntax-ns#";
        public const string OwlPrefix = "owl";
        public const string OwlUri = "http://www.w3.org/2002/07/owl#";
        public const string XsdPrefix = "xsd";
        public const string XsdUri = "http://www.w3.org/2001/XMLSchema#";

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

        public static string GetPropertyType<T>(ILogger<T> logger, IGraph instanceModel, INode propertyNode)
        {
            var query = GetParameterizedStringQuery();

            query.CommandText = @"SELECT ?valueType WHERE {
                @property rdf:type ?bNode .
                ?bNode owl:onProperty meta:hasValue .
                ?bNode owl:onDataRange ?valueType . }";

            var propertyType = GetPropertyValueType(query, logger, instanceModel, "property", propertyNode);

            if (!string.IsNullOrEmpty(propertyType))
            {
                return propertyType;
            }

            query = GetParameterizedStringQuery();

            query.CommandText = @"SELECT ?valueType WHERE {
                @property rdf:type sosa:ObservableProperty .
                @property rdf:type ?bNode .
                ?bNode owl:onProperty meta:hasUpperLimitValue .
                ?bNode owl:onDataRange ?valueType . }";

            propertyType = GetPropertyValueType(query, logger, instanceModel, "property", propertyNode);

            if (!string.IsNullOrEmpty(propertyType))
            {
                return propertyType;
            }

            throw new Exception("The property " + propertyNode.ToString() + " was found without a value type.");
        }

        public static string GetSimpleName(string longName)
        {
            var simpleName = string.Empty;
            var simpleNameArray = longName.Split('#');

            // Check if the name URI ends with a '/' instead of a '#'.
            if (simpleNameArray.Length == 1)
            {
                simpleName = longName.Split('/')[^1];
            }
            else
            {
                simpleName = simpleNameArray[1];
            }

            return simpleName;
        }

        public static SparqlResultSet ExecuteQuery<T>(this IGraph instanceModel, SparqlParameterizedString query, ILogger<T> logger)
        {
            logger.LogInformation("Executing query: {query}", query.CommandText);

            var queryResult = (SparqlResultSet)instanceModel.ExecuteQuery(query);

            var resultString = string.Join("\n", queryResult.Results.Select(r => r.ToString()));
            logger.LogInformation("Query result: {resultString}", resultString);

            return queryResult;
        }

        private static string GetPropertyValueType<T>(SparqlParameterizedString query, ILogger<T> logger, IGraph instanceModel, string parameterName, INode propertyNode)
        {
            query.SetParameter(parameterName, propertyNode);

            var propertyTypeQueryResult = instanceModel.ExecuteQuery(query, logger);

            if (propertyTypeQueryResult.IsEmpty)
            {
                return string.Empty;
            }

            var propertyValueType = propertyTypeQueryResult.Results[0]["valueType"].ToString();
            return propertyValueType.Split('#')[1];
        }
    }
}
