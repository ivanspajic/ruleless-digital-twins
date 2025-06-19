using Models;
using Models.MapekModels;

namespace Logic.Mapek
{
    public interface IMapekExecute
    {
        public void Execute(List<Models.Action> actions, PropertyCache propertyCache);
    }
}
