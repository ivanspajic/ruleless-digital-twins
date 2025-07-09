using Logic.Models.MapekModels;

namespace Logic.Mapek
{
    internal interface IMapekExecute
    {
        public void Execute(IEnumerable<Models.OntologicalModels.Action> actions, PropertyCache propertyCache);
    }
}
