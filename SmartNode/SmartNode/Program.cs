using Logic.CaseRepository;
using Logic.FactoryInterface;
using Logic.Mapek;
using Logic.Models.DatabaseModels;
using Logic.Models.MapekModels;
using Logic.Models.OntologicalModels;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using MongoDB.Driver;
using System.CommandLine;
using System.Reflection;

namespace SmartNode
{
    internal class Program
    {
        private class EuclidMapekPlan : MapekPlan {
            public EuclidMapekPlan(IServiceProvider serviceProvider) : base(serviceProvider) {}
            protected override SimulationPath GetOptimalSimulationPath(PropertyCache propertyCache,
                    IEnumerable<OptimalCondition> optimalConditions,
                    IEnumerable<SimulationPath> simulationPaths)
            {
                return GetOptimalSimulationPathsEuclidian(simulationPaths, optimalConditions).First().Item1;
            }
        }

        static async Task Main(string[] args)
        {
            RootCommand rootCommand = new();
            Option<string> fileNameArg = new("--appsettings")
            {
                Description = "Which appsettings file to use."
            };
            rootCommand.Add(fileNameArg);
            ParseResult parseResult = rootCommand.Parse(args);
            string? settingsFile = parseResult.GetValue(fileNameArg);

            var appSettings = settingsFile == null ? Path.Combine("Properties", $"appsettings.json") : settingsFile;

            var builder = Host.CreateApplicationBuilder(args);
            builder.Configuration.AddJsonFile(appSettings);

            var filepathArguments = builder.Configuration.GetSection("FilepathArguments").Get<FilepathArguments>();
            var coordinatorSettings = builder.Configuration.GetSection("CoordinatorSettings").Get<CoordinatorSettings>();
            var databaseSettings = builder.Configuration.GetSection("DatabaseSettings").Get<DatabaseSettings>();

            var rootDirectory = Directory.GetParent(Assembly.GetExecutingAssembly().Location)!.Parent!.Parent!.Parent!.Parent!.Parent!.FullName;

            // Fix full paths.
            filepathArguments!.OntologyFilepath = Path.GetFullPath(Path.Combine(rootDirectory, filepathArguments.OntologyFilepath));
            filepathArguments.FmuDirectory = Path.GetFullPath(Path.Combine(rootDirectory, filepathArguments.FmuDirectory));
            filepathArguments.DataDirectory = Path.GetFullPath(Path.Combine(rootDirectory, filepathArguments.DataDirectory));
            filepathArguments.InferenceRulesFilepath = Path.GetFullPath(Path.Combine(rootDirectory, filepathArguments.InferenceRulesFilepath));
            filepathArguments.InstanceModelFilepath = Path.GetFullPath(Path.Combine(rootDirectory, filepathArguments.InstanceModelFilepath));
            filepathArguments.InferredModelFilepath = Path.GetFullPath(Path.Combine(rootDirectory, filepathArguments.InferredModelFilepath));
            filepathArguments.InferenceEngineFilepath = Path.GetFullPath(Path.Combine(rootDirectory, filepathArguments.InferenceEngineFilepath));

            // Register services here.
            builder.Services.AddLogging(loggingBuilder =>
            {
                loggingBuilder.AddConsole(options => options.TimestampFormat = "HH:mm:ss ");
            });
            builder.Services.AddSingleton(filepathArguments);
            builder.Services.AddSingleton(coordinatorSettings!);
            builder.Services.AddSingleton(databaseSettings!);
            // Register a factory to allow for dynamic constructor argument passing through DI.
            builder.Services.AddSingleton<IMongoClient, MongoClient>(serviceProvider => new MongoClient(databaseSettings!.ConnectionString));
            builder.Services.AddSingleton<ICaseRepository, CaseRepository>(serviceProvider => new CaseRepository(serviceProvider));
            builder.Services.AddSingleton<IFactory, Factory>(serviceProvider => new Factory(coordinatorSettings!.Environment));
            builder.Services.AddSingleton<IMapekMonitor, MapekMonitor>(serviceProvider => new MapekMonitor(serviceProvider));
            if (coordinatorSettings!.UseEuclid) {
                builder.Services.AddSingleton<IMapekPlan, MapekPlan>(serviceProvider => new EuclidMapekPlan(serviceProvider));
            } else {
                builder.Services.AddSingleton<IMapekPlan, MapekPlan>(serviceProvider => new MapekPlan(serviceProvider));
            }
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
                // Fire and forget.
                await mapekManager.StartLoop();
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
