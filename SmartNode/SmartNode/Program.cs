using Logic.Mapek;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Logic.FactoryInterface;
using System.CommandLine;
using System.Reflection;

namespace SmartNode {
    internal class Program {
        static void Main(string[] args) {
            // Parse the command line arguments.
            RootCommand rootCommand = new();

            Argument<FileInfo> fileNameArg = new("file")
            {
                Description = "RDF instance model"
            };
            Argument<string> fmuDirectoryFilepathArgument = new("fmuDirectory")
            {
                Description = "Directory containing FMUs."
            };

            Option<int> maxRoundOption = new("--round", "-r")
            {
                Description = "Maximum number of rounds for MAPE-K loop.",
                DefaultValueFactory = parseResult => 4,
            };

            Option<bool> simulateTwinningTargetOption = new("--simulate", "-s")
            {
                DefaultValueFactory = parseResult => false,
                Description = "Simulate the twinning target."
            };

            rootCommand.Add(maxRoundOption);
            rootCommand.Add(simulateTwinningTargetOption);
            rootCommand.Add(fileNameArg);
            rootCommand.Add(fmuDirectoryFilepathArgument);

            ParseResult parseResult = rootCommand.Parse(args);

            var maxRound = parseResult.GetValue(maxRoundOption);
            var simulateTwinningTarget = parseResult.GetValue(simulateTwinningTargetOption);
            var modelFile = parseResult.GetValue(fileNameArg);
            var fmuDirectory = parseResult.GetValue(fmuDirectoryFilepathArgument);

            if (parseResult.Errors.Count != 0 ||
                modelFile is not FileInfo parsedFile ||
                string.IsNullOrEmpty(fmuDirectory)) {
                throw new ArgumentException(parseResult.Errors[0].Message); // Are there always errors here?
            }

            var builder = Host.CreateApplicationBuilder(args);

            // Register services here.
            builder.Services.AddLogging(loggingBuilder => {
                loggingBuilder.AddConsole(options => options.TimestampFormat = "HH:mm:ss ");
            });
            builder.Services.AddSingleton<IMapekManager, MapekManager>(serviceprovider =>{
                return new MapekManager(serviceprovider, simulateTwinningTarget);
            });
            // Register a factory to allow for dynamic constructor argument passing through DI.
            builder.Services.AddSingleton<IFactory, Factory>(serviceProvider => new Factory(simulateTwinningTarget));

            using var host = builder.Build();

            var logger = host.Services.GetRequiredService<ILogger<Program>>();

            // Get an instance of the MAPE-K manager.
            var mapekManager = host.Services.GetRequiredService<IMapekManager>();

            // TODO: i have a hunch this is making it work for docker without other filepaths specified. theoretically, we shouldn't need it
            // For native:
            // Get executing assembly path.
            var executingAssemblyPath = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
            // Combine it with the relative path of the inferred model file.
            var modelFilePath = Path.Combine(executingAssemblyPath!, modelFile.FullName);

            // Make it system-agnostic.
            modelFilePath = Path.GetFullPath(modelFile.FullName);

            // Start the loop.
            try {
                mapekManager.StartLoop(modelFilePath, fmuDirectory, maxRound);
            }
            catch (Exception exception) {
                logger.LogCritical(exception, "Exception");
                throw;
            }

            // XXX review
            // host.Run();
            logger.LogInformation("MAPE-K ended.");
        }
    }
}
