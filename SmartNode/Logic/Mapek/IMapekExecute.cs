using Logic.Models.MapekModels;

namespace Logic.Mapek
{
    public interface IMapekExecute
    {
        public void Execute(IEnumerable<Models.OntologicalModels.Action> actions, PropertyCache propertyCache);
    }
}
