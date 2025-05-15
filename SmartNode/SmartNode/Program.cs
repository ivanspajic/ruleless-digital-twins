using Logic;
using Logic.DeviceInterfaces;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using SensorActuatorImplementations;

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
            // Register a sensor factory to allow for dynamic constructor argument passing through DI.
            builder.Services.AddSingleton(serviceProvider =>
            {
                // In theory, this function can return various Sensors depending on the input name.
                return new Func<string, string, ISensor>((sensorName, procedureName) =>
                {
                    return new ExampleSensor
                    {
                        SensorName = sensorName,
                        ProcedureName = procedureName
                    };
                });
            });

            using var host = builder.Build();

            var mapekManager = host.Services.GetRequiredService<IMapekManager>();
            // TODO: get rid of the hard-coded string.
            mapekManager.StartLoop(@"C:\dev\dt-code-generation\models-and-rules\inferred-model-1.ttl");

            host.Run();
        }
    }
}
