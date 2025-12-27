using Logic.FactoryInterface;
using Logic.Mapek;
using Logic.Models.MapekModels;
using Microsoft.Extensions.Logging;
using System.Reflection;

namespace TestProject.Mocks
{
    internal class ServiceProviderMock : IServiceProvider
    {
        private static readonly string _rootDirectoryPath = Directory.GetParent(Assembly.GetExecutingAssembly().Location)!.Parent!.FullName;

        private readonly Dictionary<Type, object?> _serviceImplementationMocks;
        public ServiceProviderMock(string model, string inferred, IFactory? factory) {
            _serviceImplementationMocks = new() {
            { typeof(ILogger<IMapekPlan>), new LoggerMock<IMapekPlan>() },
            { typeof(ILogger<IMapekKnowledge>), new LoggerMock<IMapekKnowledge>() },
            { typeof(ILogger<IMapekMonitor>), new LoggerMock<IMapekMonitor>() },
            { typeof(IFactory), factory == null ? new FactoryMock() : factory },
            { typeof(FilepathArguments), new FilepathArguments {
                InferenceEngineFilepath = Path.Combine(_rootDirectoryPath, "SmartNode", "ModelsAndRules", "ruleless-digital-twins-inference-engine.jar"),
                OntologyFilepath = Path.Combine(_rootDirectoryPath, "SmartNode", "Ontology", "ruleless-digital-twins.ttl"),
                InstanceModelFilepath = model,
                InferenceRulesFilepath = Path.Combine(_rootDirectoryPath, "SmartNode", "ModelsAndRules", "inference-rules.rules"),
                InferredModelFilepath = inferred,
                FmuDirectory = Path.Combine(_rootDirectoryPath, "Implementations", "FMUs"),
                DataDirectory = Path.Combine(_rootDirectoryPath, "SmartNode", "StateData")
            } },
            // { typeof(IMapekKnowledge), new MapekKnowledge(this) },
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

        public void Add(System.Type o, object k) { // TODO: Review
            _serviceImplementationMocks.Add(o, k);
        }
    }
}
