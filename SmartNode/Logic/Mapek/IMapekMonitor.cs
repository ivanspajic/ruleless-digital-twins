using Logic.Models.MapekModels;
using VDS.RDF;

namespace Logic.Mapek
{
    internal interface IMapekMonitor
    {
        public PropertyCache Monitor(IGraph instanceModel);
    }
}
