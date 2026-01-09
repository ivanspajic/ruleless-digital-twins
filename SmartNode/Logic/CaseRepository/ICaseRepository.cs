using Logic.Models.DatabaseModels;
using Logic.Models.OntologicalModels;

namespace Logic.CaseRepository {
    public interface ICaseRepository {
        public Case ReadCase(IEnumerable<Property> quantizedProperties,
            IEnumerable<OptimalCondition> quantizedOptimalConditions,
            int lookAheadCycles,
            int simulationDurationSeconds,
            int caseIndex);

        public void CreateCase(Case caseToCreate);
    }
}
