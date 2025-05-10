using Logic;
using Logic.DeviceInterfaces;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
using SensorActuatorImplementations;

namespace SmartNode
{
    public class Program
    {
        public static async Task Main(string[] args)
        {
            var builder = WebAssemblyHostBuilder.CreateDefault(args);
            builder.RootComponents.Add<App>("#app");
            builder.RootComponents.Add<HeadOutlet>("head::after");

            builder.Services.AddScoped(sp => new HttpClient { BaseAddress = new Uri(builder.HostEnvironment.BaseAddress) });

            // Add all the required services.
            builder.Services.AddLogging();
            builder.Services.AddSingleton<IMapekManager, MapekManager>(serviceProvider =>
            {
                return new MapekManager(serviceProvider);
            });
            builder.Services.AddSingleton(serviceProvider =>
            {
                return new Func<string, ISensor>(name =>
                {
                    return new ExampleSensorDoubleValues
                    {
                        Name = name
                    };
                });
            });

            var webAssemblyHost = builder.Build();

            // Instantiate the MAPE-K loop.
            var mapekManager = webAssemblyHost.Services.GetRequiredService<IMapekManager>();
            mapekManager.StartLoop("C:/dev/dt-code-generation/models-and-rules/inferred-model-1.ttl");

            await webAssemblyHost.RunAsync();
        }
    }
}
