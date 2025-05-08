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
            // TODO: we need to register a factory for our sensors here

            var webAssemblyHost = builder.Build();

            // Instantiate the MAPE-K loop.
            webAssemblyHost.Services.GetRequiredService<IMapekManager>();

            await webAssemblyHost.RunAsync();
        }
    }
}
