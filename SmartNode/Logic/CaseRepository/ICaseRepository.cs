using Logic.Models.DatabaseModels;
using Logic.Models.OntologicalModels;

namespace Logic.CaseRepository {
    public interface ICaseRepository {
        public Case ReadCase(IEnumerable<Property> properties,
            IEnumerable<OptimalCondition> optimalConditions,
            int lookAheadCycles,
            int simulationDurationSeconds,
            int caseIndex,
            double propertyValueFuzziness);

        public void CreateCase(Case caseToCreate);
    }
}
