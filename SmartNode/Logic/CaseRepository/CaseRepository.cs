using Logic.Mapek.Comparers;
using Logic.Models.DatabaseModels;
using Logic.Models.OntologicalModels;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using MongoDB.Driver;

namespace Logic.CaseRepository {
    public class CaseRepository : ICaseRepository {
        private readonly IMongoCollection<Case> _caseCollection;
        private readonly ILogger<ICaseRepository> _logger;

        public CaseRepository(IServiceProvider serviceProvider) {
            _logger = serviceProvider.GetRequiredService<ILogger<ICaseRepository>>();

            var databaseSettings = serviceProvider.GetRequiredService<DatabaseSettings>();
            var mongoClient = serviceProvider.GetRequiredService<IMongoClient>();
            var mongoDatabase = mongoClient.GetDatabase(databaseSettings.DatabaseName);
            _caseCollection = mongoDatabase.GetCollection<Case>(databaseSettings.CollectionName);
        }

        public Case ReadCase(IEnumerable<Property> properties,
            IEnumerable<OptimalCondition> optimalConditions,
            int lookAheadCycles,
            int cycleDurationSeconds,
            int caseIndex,
            double propertyValueFuzziness) {
            var potentialMatches = _caseCollection.Find(element =>
                element.LookAheadCycles == lookAheadCycles &&
                element.CycleDurationSeconds == cycleDurationSeconds &&
                element.Index == caseIndex)
                .ToEnumerable();

            var match = potentialMatches.Where(element => {
                var propertySet = new HashSet<Property>(properties, new FuzzyPropertyEqualityComparer(propertyValueFuzziness));
                var optimalConditionSet = new HashSet<OptimalCondition>(optimalConditions, new OptimalConditionEqualityComparer(propertyValueFuzziness));

                return propertySet.SetEquals(element.QuantizedProperties) && optimalConditionSet.SetEquals(element.QuantizedOptimalConditions);
            }).FirstOrDefault()!;

            if (match is not null) {
                _logger.LogInformation("Matching case found.");
            } else {
                _logger.LogInformation("No matching case found.");
            }

            return match!;
        }

        public void CreateCase(Case caseToCreate) {
            _logger.LogInformation("Saving a new case."); // This should probably include some information from the case. What exactly?

            _caseCollection.InsertOne(caseToCreate);
        }
    }
}
