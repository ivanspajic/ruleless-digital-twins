using Logic.Models.DatabaseModels;
using MongoDB.Driver;

namespace Logic.CaseDatabaseRepository {
    public class CaseDatabaseRepository : ICaseDatabaseRepository {
        private readonly IMongoCollection<Case> _caseCollection;

        public CaseDatabaseRepository(DatabaseSettings databaseSettings) {
            var mongoClient = new MongoClient(databaseSettings.ConnectionString);
            var mongoDatabase = mongoClient.GetDatabase(databaseSettings.DatabaseName);

            _caseCollection = mongoDatabase.GetCollection<Case>(databaseSettings.CollectionName);
        }

        public Case GetCase() {
            return _caseCollection.Find(element => true).FirstOrDefault(); // TODO: finish this comparison.
        }

        public void CreateCase(Case caseToCreate) {
            _caseCollection.InsertOne(caseToCreate);
        }
    }
}
