using Models.MapekModels;
using VDS.RDF;

namespace Logic.Mapek
{
    public interface IMapekMonitor
    {
        public PropertyCache Monitor(IGraph instanceModel);
    }
}
