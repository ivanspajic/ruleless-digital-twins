using Logic.Models.MapekModels;

namespace Logic.Mapek
{
    public interface IMapekMonitor
    {
        public Task<Cache> Monitor();
    }
}
