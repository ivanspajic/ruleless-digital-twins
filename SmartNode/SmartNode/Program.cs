using Logic.Mapek;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Logic.FactoryInterface;
using System.Reflection;
using Logic.Mapek.EqualityComparers;

namespace SmartNode
{
    internal class Program
    {
        static void Main(string[] args)
        {
            var builder = Host.CreateApplicationBuilder(args);

            // Register services here.
            builder.Services.AddLogging(loggingBuilder =>
            {
                loggingBuilder.AddConsole();
            });
            builder.Services.AddSingleton<IMapekManager, MapekManager>(serviceprovider =>
            {
                return new MapekManager(serviceprovider);
            });
            // Register a factory to allow for dynamic constructor argument passing through DI.
            builder.Services.AddSingleton<IFactory, Factory>();
            builder.Services.AddSingleton<IMapekMonitor, MapekMonitor>();
            builder.Services.AddSingleton<IMapekAnalyze, MapekAnalyze>();
            builder.Services.AddSingleton<IMapekPlan, MapekPlan>();
            builder.Services.AddSingleton<IMapekExecute, MapekExecute>();
            builder.Services.AddSingleton<IEqualityComparer<HashSet<Models.OntologicalModels.Action>>, ActionSetEqualityComparer>();

            using var host = builder.Build();

            var logger = host.Services.GetRequiredService<ILogger<Program>>();

            // Get an instance of the MAPE-K manager.
            var mapekManager = host.Services.GetRequiredService<IMapekManager>();

            // Get executing assembly path.
            var executingAssemblyPath = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
            // Combine it with the relative path of the inferred model file.
            var modelFilePath = Path.Combine(executingAssemblyPath, @"..\..\..\..\..\models-and-rules\inferred-model-1.ttl");
            // Make it system-agnostic.
            modelFilePath = Path.GetFullPath(modelFilePath);

            // Start the loop.
            try
            {
                mapekManager.StartLoop(modelFilePath);
            }
            catch (Exception exception)
            {
                logger.LogCritical(exception, "Exception");

                throw;
            }

            host.Run();
        }
    }
}
