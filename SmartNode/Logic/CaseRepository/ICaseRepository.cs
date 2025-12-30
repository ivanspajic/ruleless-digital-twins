using Logic.Models.DatabaseModels;
using Logic.Models.OntologicalModels;

namespace Logic.CaseRepository {
    public interface ICaseRepository {
        public Case GetCase(IEnumerable<Property> quantizedProperties,
            IEnumerable<OptimalCondition> quantizedOptimalConditions,
            int lookAheadCycles,
            int simulationDurationSeconds,
            int index);

        public void CreateCase(Case caseToCreate);
    }
}
