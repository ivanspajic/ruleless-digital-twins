using Logic.FactoryInterface;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Models;

namespace Logic.Mapek
{
    public class MapekExecute : IMapekExecute
    {
        private readonly ILogger<MapekExecute> _logger;
        private readonly IFactory _factory;

        public MapekExecute(IServiceProvider serviceProvider)
        {
            _logger = serviceProvider.GetRequiredService<ILogger<MapekExecute>>();
            _factory = serviceProvider.GetRequiredService<IFactory>();
        }

        public void Execute(Plan plan)
        {
            // for every action in the plan, either actuate the right actuators or adjust the right configurableparameters
        }
    }
}
