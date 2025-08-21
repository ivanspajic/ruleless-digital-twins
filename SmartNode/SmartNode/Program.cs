using Logic.Mapek;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Logic.FactoryInterface;
using System.Reflection;

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

            using var host = builder.Build();

            var logger = host.Services.GetRequiredService<ILogger<Program>>();

            // Get an instance of the MAPE-K manager.
            var mapekManager = host.Services.GetRequiredService<IMapekManager>();

            var modelFile = "inferred-model-2.ttl";

            //// For Windows:
            //// Get executing assembly path.
            //var executingAssemblyPath = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
            //// Combine it with the relative path of the inferred model file.
            //var modelFilePath = Path.Combine(executingAssemblyPath!, modelFile);
            //// Make it system-agnostic.
            //modelFilePath = Path.GetFullPath(modelFile);

            // For Docker:
            var modelFilePath = modelFile;

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
