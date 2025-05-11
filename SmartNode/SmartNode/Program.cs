using Logic;
using Logic.DeviceInterfaces;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using SensorActuatorImplementations;
using System.Numerics;

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
                return new Func<string, ISensor>(sensorName =>
                {
                    return new ExampleSensor
                    {
                        Name = sensorName
                    };
                });
            });

            using var host = builder.Build();

            var mapekManager = host.Services.GetRequiredService<IMapekManager>();
            mapekManager.StartLoop(@"C:\dev\dt-code-generation\models-and-rules\inferred-model-1.ttl");

            host.Run();
        }
    }
}
