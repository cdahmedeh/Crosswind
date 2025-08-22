// See https://aka.ms/new-console-template for more information

using LegacySimBridge;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

class Program
{
    static void Main(string[] args)
    {
        Console.WriteLine("Welcome To LegacySimBridge!");

        var services = new ServiceCollection();
        services.AddLogging(b => b.AddSimpleConsole().SetMinimumLevel(LogLevel.Information));
        services.AddTransient<FSUIPCService>();

        var provider = services.BuildServiceProvider();
        
        var service = provider.GetRequiredService<FSUIPCService>();
        service.Connect();

        while (true)
        {
            service.Refresh();
            
            Console.WriteLine(service.GetCoordinates());
            Console.WriteLine(service.GetSquawkCode());
            Console.WriteLine(service.GetSpeeds());
            Console.WriteLine(service.GetAltitude());
            Console.WriteLine(service.GetHeading());
            
            Thread.Sleep(1000);
        }
    }
}

