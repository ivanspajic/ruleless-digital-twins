using Logic.Models.MapekModels;
using Logic.Models.OntologicalModels;
using VDS.RDF.Query;

namespace Logic.Mapek {
    public interface IMapekKnowledge {
        public SparqlResultSet ExecuteQuery(string queryString);

        public static SparqlParameterizedString GetParameterizedStringQuery(string queryString)
        {
            var query = new SparqlParameterizedString {
                CommandText = queryString
            };

            // Register the relevant prefixes for the queries to come.
            query.Namespaces.AddNamespace(DtPrefix, new Uri(DtUri));
            query.Namespaces.AddNamespace(SosaPrefix, new Uri(SosaUri));
            query.Namespaces.AddNamespace(SsnPrefix, new Uri(SsnUri));
            query.Namespaces.AddNamespace(RdfPrefix, new Uri(RdfUri));
            query.Namespaces.AddNamespace(OwlPrefix, new Uri(OwlUri));
            query.Namespaces.AddNamespace(XsdPrefix, new Uri(XsdUri));

            return query;
        }

        public SparqlResultSet ExecuteQuery(SparqlParameterizedString query, bool useInferredModel = false);

        public void UpdatePropertyValue(Property property);

        public void UpdateConfigurableParameterValue(ConfigurableParameter configurableParameter);

        public void CommitInMemoryInstanceModelToKnowledgeBase();

        public void LoadModelsFromKnowledgeBase();

        public void UpdateModel(SparqlParameterizedString query);
        IEnumerable<OptimalCondition> GetAllOptimalConditions(PropertyCache propertyCache);

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
    }
}
