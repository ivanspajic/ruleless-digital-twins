using Logic.CaseRepository;
using Logic.Models.DatabaseModels;
using Logic.Models.OntologicalModels;

namespace TestProject.Mocks.ServiceMocks {
    public class CaseRepositoryMock : ICaseRepository {
        public CaseRepositoryMock() {
            Cases = [];
        }

        public List<Case> Cases { get; private set; }

        public void CreateCase(Case caseToCreate) {
            Cases.Add(caseToCreate);
        }

        public Case ReadCase(IEnumerable<Property> quantizedProperties,
            IEnumerable<OptimalCondition> quantizedOptimalConditions,
            int lookAheadCycles,
            int simulationDurationSeconds,
            int index) {
            return Cases.Where(element =>
                element.QuantizedProperties!.SequenceEqual(quantizedProperties) &&
                element.QuantizedOptimalConditions!.SequenceEqual(quantizedOptimalConditions) &&
                element.Index == index &&
                element.LookAheadCycles == lookAheadCycles &&
                element.SimulationDurationSeconds == simulationDurationSeconds)
                .FirstOrDefault()!;
        }
    }
}
