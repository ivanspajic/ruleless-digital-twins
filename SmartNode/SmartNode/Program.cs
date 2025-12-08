using Logic.FactoryInterface;
using Logic.Mapek;
using Logic.Models.MapekModels;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System.CommandLine;
using System.Reflection;

namespace SmartNode
{
    internal class Program
    {
        static void Main(string[] args)
        {
            // Parse the command line arguments.
            var rootCommand = new RootCommand();

            var inferenceEngineArgument = new Argument<FileInfo>("inferenceEngine") {
                Description = "Inference engine."
            };
            var ontologyArgument = new Argument<FileInfo>("ontology") {
                Description = "Ontology."
            };
            var instanceModelArgument = new Argument<FileInfo>("instanceModel") {
                Description = "TT instance model."
            };
            var inferenceRulesArgument = new Argument<FileInfo>("inferenceRules") {
                Description = "Inference rules."
            };
            var inferredModelArgument = new Argument<FileInfo>("inferredModel") {
                Description = "TT inferred model."
            };
            var fmuDirectoryArgument = new Argument<string>("fmuDirectory") {
                Description = "Directory containing FMUs."
            };
            var dataDirectoryArgument = new Argument<string>("dataDirectory") {
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

            rootCommand.Add(inferenceEngineArgument);
            rootCommand.Add(ontologyArgument);
            rootCommand.Add(instanceModelArgument);
            rootCommand.Add(inferenceRulesArgument);
            rootCommand.Add(inferredModelArgument);
            rootCommand.Add(fmuDirectoryArgument);
            rootCommand.Add(dataDirectoryArgument);
            rootCommand.Add(maxRoundOption);
            rootCommand.Add(simulateTwinningTargetOption);

            ParseResult parseResult = rootCommand.Parse(args);

            var inferenceEngineFile = parseResult.GetValue(inferenceEngineArgument);
            var ontologyFile = parseResult.GetValue(ontologyArgument);
            var instanceModelFile = parseResult.GetValue(instanceModelArgument);
            var inferenceRulesFile = parseResult.GetValue(inferenceRulesArgument);
            var inferredModelFile = parseResult.GetValue(inferredModelArgument);
            var fmuDirectory = parseResult.GetValue(fmuDirectoryArgument);
            var dataDirectory = parseResult.GetValue(dataDirectoryArgument);
            var maxRound = parseResult.GetValue(maxRoundOption);
            var simulateTwinningTarget = parseResult.GetValue(simulateTwinningTargetOption);

            if (parseResult.Errors.Count != 0 ||
                inferenceEngineFile is not FileInfo inferenceEngine ||
                ontologyFile is not FileInfo ontology ||
                instanceModelFile is not FileInfo parsedFile ||
                inferenceRulesFile is not FileInfo inferenceRules ||
                inferredModelFile is not FileInfo inferredModel ||
                string.IsNullOrEmpty(fmuDirectory) ||
                string.IsNullOrEmpty(dataDirectory))
            {
                throw new ArgumentException(parseResult.Errors[0].Message); // Are there always errors here?
            }

            // TODO: i have a hunch this is making it work for docker without other filepaths specified. theoretically, we shouldn't need it
            // For native:
            // Get executing assembly path.
            var executingAssemblyPath = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
            var inferenceEngineFilePath = Path.Combine(executingAssemblyPath!, inferenceEngineFile.FullName);
            var ontologyFilePath = Path.Combine(executingAssemblyPath!, ontologyFile.FullName);
            var instanceModelFilePath = Path.Combine(executingAssemblyPath!, instanceModelFile.FullName);
            var inferenceRulesFilePath = Path.Combine(executingAssemblyPath!, inferenceRulesFile.FullName);
            var inferredModelFilePath = Path.Combine(executingAssemblyPath!, inferredModelFile.FullName);

            // Make it system-agnostic and wrap it into a POCO.
            var filepathArguments = new FilepathArguments {
                InferenceEngineFilepath = Path.GetFullPath(inferenceEngineFile.FullName),
                OntologyFilepath = Path.GetFullPath(ontologyFile.FullName),
                InstanceModelFilepath = Path.GetFullPath(instanceModelFile.FullName),
                InferenceRulesFilepath = Path.GetFullPath(inferenceRulesFile.FullName),
                InferredModelFilepath = Path.GetFullPath(inferredModelFile.FullName),
                FmuDirectory = Path.GetFullPath(fmuDirectory),
                DataDirectory = Path.GetFullPath(dataDirectory)
            };

            var builder = Host.CreateApplicationBuilder(args);

            // Register services here.
            builder.Services.AddLogging(loggingBuilder =>
            {
                loggingBuilder.AddConsole(options => options.TimestampFormat = "HH:mm:ss ");
            });
            builder.Services.AddSingleton(filepathArguments);
            // Register a factory to allow for dynamic constructor argument passing through DI.
            builder.Services.AddSingleton<IFactory, Factory>(serviceProvider => new Factory(simulateTwinningTarget));
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
                mapekManager.StartLoop(instanceModelFilePath, fmuDirectory, dataDirectory, maxRound, simulateTwinningTarget);
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
