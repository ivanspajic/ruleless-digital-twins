using Logic.Mapek;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Logic.FactoryInterface;
using System.CommandLine;
using System.Reflection;

namespace SmartNode
{
    internal class Program
    {
        static void Main(string[] args)
        {
            // Command line parsind:
            RootCommand rootCommand = new();
            Argument<FileInfo> fileNameArg = new("file")
            {
                Description = "RDF instance model."
            };
            Option<int> maxRoundOption = new("--round", "-r")
            {
                Description = "Maximum number of rounds for MAPE-K loop.",
                DefaultValueFactory = parseResult => 4,
            };
            rootCommand.Add(maxRoundOption);
            rootCommand.Add(fileNameArg);
            ParseResult parseResult = rootCommand.Parse(args);
            String modelFile;
            int maxRound;
            if (parseResult.Errors.Count == 0 && parseResult.GetValue(fileNameArg) is FileInfo parsedFile)
            {
                modelFile = parsedFile.FullName;
                maxRound = parseResult.GetValue(maxRoundOption);
            }
            else
            {
                throw new ArgumentException(parseResult.Errors[0].Message);
            }

            var builder = Host.CreateApplicationBuilder(args);

            // Register services here.
            builder.Services.AddLogging(loggingBuilder =>
            {
                loggingBuilder.AddConsole(options =>
                { options.TimestampFormat = "HH:mm:ss "; });
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

            // For native:
            // Get executing assembly path.
            var executingAssemblyPath = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
            // Combine it with the relative path of the inferred model file.
            var modelFilePath = Path.Combine(executingAssemblyPath!, modelFile);
            // Make it system-agnostic.
            modelFilePath = Path.GetFullPath(modelFile);

            // Start the loop.
            try
            {
                mapekManager.StartLoop(modelFilePath, maxRound);
            }
            catch (Exception exception)
            {
                logger.LogCritical(exception, "Exception");

                throw;
            }

            // XXX review
            // host.Run();
        }
    }
}
