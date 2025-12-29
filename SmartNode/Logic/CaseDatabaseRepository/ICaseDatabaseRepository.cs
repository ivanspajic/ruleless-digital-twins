using Logic.Models.DatabaseModels;

namespace Logic.CaseDatabaseRepository {
    public interface ICaseDatabaseRepository {
        public Case GetCase();

        public void CreateCase(Case caseToCreate);
    }
}
