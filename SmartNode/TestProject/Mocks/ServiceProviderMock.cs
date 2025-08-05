using Logic.FactoryInterface;
using Logic.Mapek;
using Microsoft.Extensions.Logging;

namespace TestProject.Mocks
{
    internal class ServiceProviderMock : IServiceProvider
    {
        private Dictionary<Type, object?> _serviceImplementationMocks = new()
        {
            { typeof(ILogger<MapekAnalyze>), new LoggerMock<MapekAnalyze>() },
            { typeof(IFactory), new FactoryMock() }
        };

        public object? GetService(Type serviceType)
        {
            if (_serviceImplementationMocks.TryGetValue(serviceType, out object? implementation))
            {
                return implementation;
            }

            return null;
        }
    }
}
