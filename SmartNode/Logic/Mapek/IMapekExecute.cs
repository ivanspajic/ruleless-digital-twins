using Models.MapekModels;

namespace Logic.Mapek
{
    public interface IMapekExecute
    {
        public void Execute(IEnumerable<Models.Action> actions, PropertyCache propertyCache);
    }
}
