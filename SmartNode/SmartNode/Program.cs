using Logic.FactoryInterface;
using Logic.Mapek;
using Logic.Models.MapekModels;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System.Reflection;

namespace SmartNode
{
    internal class Program
    {
        static void Main(string[] args)
        {
            var builder = Host.CreateApplicationBuilder(args);

            builder.Configuration.AddJsonFile(Path.Combine("Properties", $"appsettings.json"));

            var filepathArguments = builder.Configuration.GetSection("FilepathArguments").Get<FilepathArguments>();
            var coordinatorSettings = builder.Configuration.GetSection("CoordinatorSettings").Get<CoordinatorSettings>();

            // Fix full paths.
            filepathArguments!.OntologyFilepath = Path.GetFullPath(Path.Combine(Directory.GetParent(Assembly.GetExecutingAssembly().Location)!.FullName, filepathArguments.OntologyFilepath));
            filepathArguments.FmuDirectory = Path.GetFullPath(Path.Combine(Directory.GetParent(Assembly.GetExecutingAssembly().Location)!.FullName, filepathArguments.FmuDirectory));
            filepathArguments.DataDirectory = Path.GetFullPath(Path.Combine(Directory.GetParent(Assembly.GetExecutingAssembly().Location)!.FullName, filepathArguments.DataDirectory));
            filepathArguments.InferenceRulesFilepath = Path.GetFullPath(Path.Combine(Directory.GetParent(Assembly.GetExecutingAssembly().Location)!.FullName, filepathArguments.InferenceRulesFilepath));
            filepathArguments.InstanceModelFilepath = Path.GetFullPath(Path.Combine(Directory.GetParent(Assembly.GetExecutingAssembly().Location)!.FullName, filepathArguments.InstanceModelFilepath));
            filepathArguments.InferredModelFilepath = Path.GetFullPath(Path.Combine(Directory.GetParent(Assembly.GetExecutingAssembly().Location)!.FullName, filepathArguments.InferredModelFilepath));
            filepathArguments.InferenceEngineFilepath = Path.GetFullPath(Path.Combine(Directory.GetParent(Assembly.GetExecutingAssembly().Location)!.FullName, filepathArguments.InferenceEngineFilepath));

            // Register services here.
            builder.Services.AddLogging(loggingBuilder =>
            {
                loggingBuilder.AddConsole(options => options.TimestampFormat = "HH:mm:ss ");
            });
            builder.Services.AddSingleton(filepathArguments);
            builder.Services.AddSingleton(coordinatorSettings!);
            // Register a factory to allow for dynamic constructor argument passing through DI.
            builder.Services.AddSingleton<IFactory, Factory>(serviceProvider => new Factory(coordinatorSettings!.UseSimulatedEnvironment));
            builder.Services.AddSingleton<IMapekMonitor, MapekMonitor>(serviceProvider => new MapekMonitor(serviceProvider));
            builder.Services.AddSingleton<IMapekAnalyze, MapekAnalyze>(serviceProvider => new MapekAnalyze(serviceProvider));
            builder.Services.AddSingleton<IMapekPlan, MapekPlan>(serviceProvider => new MapekPlan(serviceProvider));
            builder.Services.AddSingleton<IMapekExecute, MapekExecute>(serviceProvider => new MapekExecute(serviceProvider));
            builder.Services.AddSingleton<IMapekKnowledge, MapekKnowledge>(serviceProvider => new MapekKnowledge(serviceProvider));
            builder.Services.AddSingleton<IMapekManager, MapekManager>(serviceprovider => new MapekManager(serviceprovider));

            using var host = builder.Build();

            var logger = host.Services.GetRequiredService<ILogger<Program>>();

            // Get an instance of the MAPE-K manager.
            var mapekManager = host.Services.GetRequiredService<IMapekManager>();

            // Start the loop.
            try
            {
                mapekManager.StartLoop();
            }
            catch (Exception exception)
            {
                logger.LogCritical(exception, "Exception");
                throw;
            }

            // XXX review
            // host.Run();
            logger.LogInformation("MAPE-K ended.");
        }
    }
}
