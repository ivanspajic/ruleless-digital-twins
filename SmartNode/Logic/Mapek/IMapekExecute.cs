using Logic.Models.MapekModels;

namespace Logic.Mapek
{
    public interface IMapekExecute
    {
        public Task Execute(Simulation simulation);
    }
}
