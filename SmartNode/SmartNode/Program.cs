using Logic.FactoryInterface;
using Logic.Mapek;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System.CommandLine;
using System.Reflection;
using VDS.RDF;
using VDS.RDF.Parsing;

namespace SmartNode
{
    internal class Program
    {
        static void Main(string[] args)
        {
            // Parse the command line arguments.
            var rootCommand = new RootCommand();

            var fileNameArg = new Argument<FileInfo>("file")
            {
                Description = "RDF instance model"
            };
            var fmuDirectoryArgument = new Argument<string>("fmuDirectory")
            {
                Description = "Directory containing FMUs."
            };
            var dataDirectoryArgument = new Argument<string>("dataDirectory")
            {
                Description = "Directory for storing MAPE-K data."
            };

            var maxRoundOption = new Option<int>("--round", "-r")
            {
                Description = "Maximum number of rounds for MAPE-K loop.",
                DefaultValueFactory = parseResult => 4,
            };

            var simulateTwinningTargetOption = new Option<bool>("--simulate", "-s")
            {
                DefaultValueFactory = parseResult => false,
                Description = "Simulate the twinning target."
            };

            rootCommand.Add(maxRoundOption);
            rootCommand.Add(simulateTwinningTargetOption);
            rootCommand.Add(fileNameArg);
            rootCommand.Add(fmuDirectoryArgument);
            rootCommand.Add(dataDirectoryArgument);

            ParseResult parseResult = rootCommand.Parse(args);

            var maxRound = parseResult.GetValue(maxRoundOption);
            var simulateTwinningTarget = parseResult.GetValue(simulateTwinningTargetOption);
            var modelFile = parseResult.GetValue(fileNameArg);
            var fmuDirectory = parseResult.GetValue(fmuDirectoryArgument);
            var dataDirectory = parseResult.GetValue(dataDirectoryArgument);

            if (parseResult.Errors.Count != 0 ||
                modelFile is not FileInfo parsedFile ||
                string.IsNullOrEmpty(fmuDirectory) ||
                string.IsNullOrEmpty(dataDirectory))
            {
                throw new ArgumentException(parseResult.Errors[0].Message); // Are there always errors here?
            }

            // TODO: i have a hunch this is making it work for docker without other filepaths specified. theoretically, we shouldn't need it
            // For native:
            // Get executing assembly path.
            var executingAssemblyPath = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
            // Combine it with the relative path of the inferred model file.
            var modelFilePath = Path.Combine(executingAssemblyPath!, modelFile.FullName);

            // Make it system-agnostic.
            modelFilePath = Path.GetFullPath(modelFile.FullName);

            var builder = Host.CreateApplicationBuilder(args);

            // Register services here.
            builder.Services.AddLogging(loggingBuilder =>
            {
                loggingBuilder.AddConsole(options => options.TimestampFormat = "HH:mm:ss ");
            });
            // Register a factory to allow for dynamic constructor argument passing through DI.
            builder.Services.AddSingleton<IFactory, Factory>(serviceProvider => new Factory(simulateTwinningTarget));
            builder.Services.AddSingleton<IMapekMonitor, MapekMonitor>(serviceProvider => new MapekMonitor(serviceProvider));
            builder.Services.AddSingleton<IMapekAnalyze, MapekAnalyze>(serviceProvider => new MapekAnalyze(serviceProvider));
            builder.Services.AddSingleton<IMapekPlan, MapekPlan>(serviceProvider => new MapekPlan(serviceProvider));
            builder.Services.AddSingleton<IMapekExecute, MapekExecute>(serviceProvider => new MapekExecute(serviceProvider));
            builder.Services.AddSingleton<IMapekKnowledge, MapekKnowledge>(serviceProvider => new MapekKnowledge(serviceProvider, modelFilePath));
            builder.Services.AddSingleton<IMapekManager, MapekManager>(serviceprovider => new MapekManager(serviceprovider));

            using var host = builder.Build();

            var logger = host.Services.GetRequiredService<ILogger<Program>>();

            // Get an instance of the MAPE-K manager.
            var mapekManager = host.Services.GetRequiredService<IMapekManager>();

            // Start the loop.
            try
            {
                mapekManager.StartLoop(modelFilePath, fmuDirectory, dataDirectory, maxRound, simulateTwinningTarget);
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
