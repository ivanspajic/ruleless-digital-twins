using Models;
using Models.MapekModels;

namespace Logic.Mapek
{
    public interface IMapekExecute
    {
        public void Execute(Plan plan, PropertyCache propertyCache);
    }
}
