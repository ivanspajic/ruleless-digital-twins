using System.Diagnostics;
using Logic.CaseRepository;
using Logic.Mapek;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace TestProject.Mocks.ServiceMocks
{
    interface IRDTServiceProvider : IServiceProvider {
        void Add<T>(T t);
        void Add<I>(Type type, I implementation);
    }
    internal class ServiceProviderMock : IRDTServiceProvider
    {
        private readonly Dictionary<Type, object?> _serviceImplementationMocks;

        public ServiceProviderMock()
        {
            _serviceImplementationMocks = new() {
                { typeof(ILogger<IMapekPlan>), new LoggerMock<IMapekPlan>() },
                { typeof(ILogger<IMapekKnowledge>), new LoggerMock<IMapekKnowledge>() },
                { typeof(ILogger<IMapekMonitor>), new LoggerMock<IMapekMonitor>() },
                { typeof(ILogger<IMapekManager>), new LoggerMock<IMapekManager>() },
                { typeof(ILogger<ICaseRepository>), new LoggerMock<ICaseRepository>() },
                { typeof(ILogger<IMapekExecute>), new LoggerMock<IMapekExecute>() }
            };
        }

        public object? GetService(Type serviceType)
        {
            if (_serviceImplementationMocks.TryGetValue(serviceType, out object? implementation))
            {
                return implementation;
            }

            throw new ArgumentException($"No instance of type {serviceType} was added to the collection.");
        }

        public void Add<T>(T t)
        {
            _serviceImplementationMocks.Add(typeof(T), t);
        }

        public void Add<I>(Type type, I implementation)
        {
            _serviceImplementationMocks.Add(type, implementation);
        }
    }
    
        internal class NullServiceProviderMock : IRDTServiceProvider {
        private readonly Dictionary<Type, object?> _serviceImplementationMocks;

        public NullServiceProviderMock() {
            _serviceImplementationMocks = new() {
                { typeof(ILogger<IMapekPlan>), new NullLogger<IMapekPlan>() },
                { typeof(ILogger<IMapekKnowledge>), new NullLogger<IMapekKnowledge>() },
                { typeof(ILogger<IMapekMonitor>), new NullLogger<IMapekMonitor>() },
                { typeof(ILogger<IMapekManager>), new NullLogger<IMapekManager>() },
                { typeof(ILogger<ICaseRepository>), new NullLogger<ICaseRepository>() },
                { typeof(ILogger<IMapekExecute>), new NullLogger<IMapekExecute>() }
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

        public void Add<I>(Type type, I implementation)
        {
            Debug.Assert(type.IsInstanceOfType(implementation));
            _serviceImplementationMocks.Add(type, implementation);
        }
    }
}
