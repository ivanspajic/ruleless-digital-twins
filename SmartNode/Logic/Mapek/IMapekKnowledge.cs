using Logic.Models.MapekModels;
using Logic.Models.OntologicalModels;
using VDS.RDF.Query;

namespace Logic.Mapek {
    public interface IMapekKnowledge {
        public SparqlResultSet ExecuteQuery(string queryString);

        public SparqlParameterizedString GetParameterizedStringQuery(string queryString);

        public SparqlResultSet ExecuteQuery(SparqlParameterizedString query, bool useInferredModel = false);

        public void UpdatePropertyValue(Property property);

        public void UpdateConfigurableParameterValue(ConfigurableParameter configurableParameter);

        public void CommitInMemoryInstanceModelToKnowledgeBase();

        public void LoadModelsFromKnowledgeBase();

        public void UpdateModel(SparqlParameterizedString query);
        IEnumerable<Condition> GetAllConditions(PropertyCache propertyCache);
    }
}
