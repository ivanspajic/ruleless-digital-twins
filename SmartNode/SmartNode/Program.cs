using Logic.Mapek;
using Logic.DeviceInterfaces;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using SensorActuatorImplementations;
using Logic.FactoryInterface;

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
            builder.Services.AddSingleton<IMapekCache, MapekCache>();

            using var host = builder.Build();

            var mapekManager = host.Services.GetRequiredService<IMapekManager>();
            // TODO: get rid of the hard-coded string.
            mapekManager.StartLoop(@"C:\dev\dt-code-generation\models-and-rules\inferred-model-1.ttl");

            host.Run();
        }
    }
}
