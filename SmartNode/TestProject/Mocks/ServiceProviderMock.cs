using Logic.FactoryInterface;
using Logic.Mapek;
using Microsoft.Extensions.Logging;

namespace TestProject.Mocks
{
    internal class ServiceProviderMock : IServiceProvider
    {
        private readonly Dictionary<Type, object?> _serviceImplementationMocks;

        public ServiceProviderMock(IFactory factory = null!) {
            _serviceImplementationMocks = new() {
                { typeof(ILogger<IMapekPlan>), new LoggerMock<IMapekPlan>() },
                { typeof(ILogger<IMapekKnowledge>), new LoggerMock<IMapekKnowledge>() },
                { typeof(ILogger<IMapekMonitor>), new LoggerMock<IMapekMonitor>() },
                { typeof(IFactory), factory ?? new FactoryMock() }
            };
        }

        public object? GetService(Type serviceType)
        {
            if (_serviceImplementationMocks.TryGetValue(serviceType, out object? implementation))
            {
                return implementation;
            }

            return null;
        }

        public void Add<T>(T t) { // TODO: Review
            _serviceImplementationMocks.Add(typeof(T), t);
        }
    }
}
