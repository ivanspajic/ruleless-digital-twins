using Logic.FactoryInterface;
using Logic.Mapek;
using Logic.Models.MapekModels;
using Microsoft.Extensions.Logging;
using System.Reflection;
using TestProject.Utilities;

namespace TestProject.Mocks
{
    internal class ServiceProviderMock : IServiceProvider
    {
        private static readonly string _rootDirectoryPath = Directory.GetParent(Assembly.GetExecutingAssembly().Location)!.Parent!.Parent!.Parent!.Parent!.Parent!.FullName;

        private readonly Dictionary<Type, object?> _serviceImplementationMocks;
        
        public ServiceProviderMock(string model, string inferred) {
            _serviceImplementationMocks = new() {
            { typeof(ILogger<IMapekPlan>), new LoggerMock<IMapekPlan>() },
            { typeof(IFactory), new FactoryMock() },
            { typeof(IMapekKnowledge), new MapekKnowledgeMock()  }, // TODO: document etc.?
            { typeof(FilepathArguments), new FilepathArguments {
                InferenceEngineFilepath = Path.Combine(_rootDirectoryPath, "models-and-rules", "ruleless-digital-twins-inference-engine.jar"),
                OntologyFilepath = Path.Combine(_rootDirectoryPath, "Ontology", "ruleless-digital-twins.ttl"),
                InstanceModelFilepath = model,
                InferenceRulesFilepath = Path.Combine(_rootDirectoryPath, "models-and-rules", "inference-rules.rules"),
                InferredModelFilepath = inferred,
                FmuDirectory = Path.Combine(_rootDirectoryPath, "SmartNode", "Implementations", "FMUs"),
                DataDirectory = Path.Combine(_rootDirectoryPath, "state-data")
            } }
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
    }
}
