using Logic.Mapek.Comparers;
using Logic.Models.DatabaseModels;
using Logic.Models.OntologicalModels;
using MongoDB.Driver;

namespace Logic.CaseRepository {
    public class CaseRepository : ICaseRepository {
        private readonly IMongoCollection<Case> _caseCollection;

        public CaseRepository(DatabaseSettings databaseSettings) {
            var mongoClient = new MongoClient(databaseSettings.ConnectionString);
            var mongoDatabase = mongoClient.GetDatabase(databaseSettings.DatabaseName);

            _caseCollection = mongoDatabase.GetCollection<Case>(databaseSettings.CollectionName);
        }

        public Case GetCase(IEnumerable<Property> quantizedProperties,
            IEnumerable<OptimalCondition> quantizedOptimalConditions,
            int lookAheadCycles,
            int simulationDurationSeconds,
            int index) {
            return _caseCollection.Find(element =>
                element.QuantizedProperties.SequenceEqual(quantizedProperties, new PropertyComparer()) &&
                element.QuantizedOptimalConditions.SequenceEqual(quantizedOptimalConditions, new OptimalConditionComparer()) &&
                element.LookAheadCycles == lookAheadCycles &&
                element.SimulationDurationSeconds == simulationDurationSeconds &&
                element.Index == index)
                .FirstOrDefault();
        }

        public void CreateCase(Case caseToCreate) {
            _caseCollection.InsertOne(caseToCreate);
        }
    }
}
