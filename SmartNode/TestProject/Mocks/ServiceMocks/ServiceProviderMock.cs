using Logic.CaseRepository;
using Logic.Mapek;
using Microsoft.Extensions.Logging;

namespace TestProject.Mocks.ServiceMocks
{
    internal class ServiceProviderMock : IServiceProvider
    {
        private readonly Dictionary<Type, object?> _serviceImplementationMocks;

        public ServiceProviderMock() {
            _serviceImplementationMocks = new() {
                { typeof(ILogger<IMapekPlan>), new LoggerMock<IMapekPlan>() },
                { typeof(ILogger<IMapekKnowledge>), new LoggerMock<IMapekKnowledge>() },
                { typeof(ILogger<IMapekMonitor>), new LoggerMock<IMapekMonitor>() },
                { typeof(ILogger<IMapekManager>), new LoggerMock<IMapekManager>() },
                { typeof(ILogger<ICaseRepository>), new LoggerMock<ICaseRepository>() },
                { typeof(ILogger<IMapekExecute>), new LoggerMock<IMapekExecute>() }
            };
        }

        public object? GetService(Type serviceType) {
            if (_serviceImplementationMocks.TryGetValue(serviceType, out object? implementation)) {
                return implementation;
            }

            throw new ArgumentException($"No instance of type {serviceType} was added to the collection.");
        }

        public void Add<T>(T t) {
            _serviceImplementationMocks.Add(typeof(T), t);
        }
    }
}
