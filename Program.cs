// See https://aka.ms/new-console-template for more information

using LegacySimBridge;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

class Program
{
    static async Task Main()
    {
        Console.WriteLine("Welcome To LegacySimBridge!");

        var services = new ServiceCollection();
        services.AddLogging(b => b.AddSimpleConsole().SetMinimumLevel(LogLevel.Information));
        services.AddTransient<FSUIPCService>();
        services.AddTransient<SimlinkService>();

        var provider = services.BuildServiceProvider();
        
        var fsService = provider.GetRequiredService<FSUIPCService>();
        fsService.Connect();

        var slService = provider.GetRequiredService<SimlinkService>();
        slService.Connect();
        
        while (true)
        {
            fsService.Refresh();
            
            var telemetry = fsService.GetTelemetry();
            Console.WriteLine(telemetry);
            slService.Send(telemetry);
            await Task.Delay(1000);
        }
    }
}

