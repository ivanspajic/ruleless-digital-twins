using Logic.Mapek;
using Microsoft.Extensions.Logging;

namespace TestProject.Mocks
{
    internal class ServiceProviderMock : IServiceProvider
    {
        private readonly Dictionary<Type, object?> _serviceImplementationMocks;

        public ServiceProviderMock() {
            _serviceImplementationMocks = new() {
                { typeof(ILogger<IMapekPlan>), new LoggerMock<IMapekPlan>() },
                { typeof(ILogger<IMapekKnowledge>), new LoggerMock<IMapekKnowledge>() },
                { typeof(ILogger<IMapekMonitor>), new LoggerMock<IMapekMonitor>() },
                { typeof(ILogger<IMapekManager>), new LoggerMock<IMapekManager>() }
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

        public void Add<T>(T t) {
            _serviceImplementationMocks.Add(typeof(T), t);
        }
    }
}
